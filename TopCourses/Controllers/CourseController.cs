﻿namespace TopCourses.Controllers
{
    using System.IO;
    using Ganss.Xss;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using MongoDB.Bson;
    using MongoDB.Driver.GridFS;
    using TopCourses.Core.Constants;
    using TopCourses.Core.Contracts;
    using TopCourses.Core.Models.ApplicationFile;
    using TopCourses.Core.Models.Course;
    using TopCourses.Infrastructure.Data.Identity;
    using TopCourses.Infrastructure.Data.MongoInterfaceses;
    using TopCourses.Models;

    public class CourseController : BaseController
    {
        private readonly ILogger logger;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ICourseService courseService;
        private readonly ICategoryService categoryService;
        private readonly ILanguageService languageService;
        private readonly IFileService fileService;
        private readonly GridFSBucket bucket;

        public CourseController(
                                ICourseService courseService,
                                ICategoryService categoryService,
                                ILanguageService languageService,
                                UserManager<ApplicationUser> userManager,
                                IFileService fileService,
                                ILogger<CourseController> logger,
                                IBucket bucketContex)
        {
            this.courseService = courseService;
            this.categoryService = categoryService;
            this.languageService = languageService;
            this.userManager = userManager;
            this.fileService = fileService;
            this.logger = logger;
            this.bucket = bucketContex.Create();
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index([FromQuery] AllCoursesQueryModel query)
        {
            var result = await this.courseService.GetAll(
                query.Category,
                query.SubCategory,
                query.SearchTerm,
                query.Language,
                query.MinPrice,
                query.MaxPrice,
                query.CurrentPage,
                AllCoursesQueryModel.CoursesPerPage,
                query.Sorting);

            var allCategories = await this.categoryService.GetAllMainCategories();

            query.Categories = allCategories.Where(c => c.ParentId == null);
            query.Languages = await this.languageService.GetAll();
            query.Courses = result;
            query.TotalCoursesCount = result.FirstOrDefault() != null ? result.FirstOrDefault().TotalCoursesCount : 0;

            return this.View(query);
        }

        public async Task<IActionResult> Add()
        {
            var categories = await this.categoryService.GetAllMainCategories();
            var languages = await this.languageService.GetAll();
            var course = new AddCourseViewModel()
            {
                Languages = languages,
                Categories = categories,
            };

            return this.View(course);
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Add(
            AddCourseViewModel model,
            [FromForm] IFormFile image)
        {
            var senitizer = new HtmlSanitizer();
            model.Title = senitizer.Sanitize(model.Title);
            model.Subtitle = senitizer.Sanitize(model.Subtitle);
            model.Description = senitizer.Sanitize(model.Description);
            model.Goals = senitizer.Sanitize(model.Goals);
            model.Requirements = senitizer.Sanitize(model.Requirements);
            foreach (var topic in model.Curriculum)
            {
                topic.Title = senitizer.Sanitize(topic.Title);
                topic.Description = senitizer.Sanitize(topic.Description);
                foreach (var video in topic.Videos)
                {
                    video.Title = senitizer.Sanitize(video.Title);
                    video.VideoUrl = senitizer.Sanitize(video.VideoUrl);
                }
            }

            if (image != null)
            {
                string[] acceptedExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".tif" };
                if (!acceptedExtensions.Contains(Path.GetExtension(image.FileName)))
                {
                    this.TempData["Error"] = "Error: Unsupported file!";
                    return this.View(model);
                }

                model.Image = await this.UploadImage(image);
            }
            else
            {
                this.TempData["Error"] = "Error: Unsupported file! File should be one of the following types: png/jpg/jpeg/gif/tif";
                return this.View(model);
            }

            var categories = await this.categoryService.GetAllMainCategories();
            if (!categories.Any(b => b.Id == model.CategoryId))
            {
                this.ModelState.AddModelError(nameof(model.CategoryId), "Category does not exist");
            }

            var languages = await this.languageService.GetAll();
            if (!languages.Any(b => b.Id == model.LanguageId))
            {
                this.ModelState.AddModelError(nameof(model.LanguageId), "Language does not exist");
            }

            foreach (var topic in model.Curriculum)
            {
                var files = await this.UploadFile(topic.Files);
                topic.FilesInfo = files;
            }

            if (!this.ModelState.IsValid)
            {
                model.Languages = languages;
                model.Categories = categories;
                return this.View(model);
            }

            var currentUserId = this.GetUserId();
            await this.courseService.CreateCourse(model, currentUserId);
            return this.RedirectToAction(nameof(this.Index));
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details([FromRoute] int id)
        {
            var course = await this.courseService.GetCourseDetails(id);
            this.ViewData["Title"] = $"{course.Title}";
            this.ViewData["Subtitle"] = $"{course.Subtitle}";
            return this.View(course);
        }

        public async Task<IActionResult> MyLearning()
        {
            var model = new MyLearningViewModel();
            var userId = this.GetUserId();
            model.CoursesEnrolled = await this.courseService.GetAllEnroledCourses(userId);
            model.CoursesCreated = await this.courseService.GetAllCreatedCourses(userId);
            model.ArchivedCourses = await this.courseService.GetAllArchivedCourses(userId);
            return this.View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromForm] int courseId)
        {
            var userId = this.GetUserId();
            await this.courseService.Delete(courseId, userId);
            return this.RedirectToAction("MyLearning");
        }

        public async Task<IActionResult> Download(string id)
        {
            var stream = await this.bucket.OpenDownloadStreamAsync(new ObjectId(id));
            var fileName = stream.FileInfo.Metadata.FirstOrDefault(x => x.Name == "FileName");
            var fileType = stream.FileInfo.Metadata.FirstOrDefault(x => x.Name == "Type");
            return this.File(stream, fileType.Value.ToString(), fileName.Value.ToString());
        }

        private async Task<ICollection<FileViewModel>> UploadFile(ICollection<IFormFile> files)
        {
            var filesToReturn = new List<FileViewModel>();
            foreach (var file in files)
            {
                try
                {
                    if (file != null && file.Length > 0)
                    {
                        var type = file.ContentType.ToString();
                        var fileName = file.FileName;
                        var options = new GridFSUploadOptions
                        {
                            Metadata = new BsonDocument { { "FileName", fileName }, { "Type", type } },
                        };

                        using (var stream = await this.bucket.OpenUploadStreamAsync(fileName, options))
                        {
                            await file.CopyToAsync(stream);

                            var fileToReturn = new FileViewModel()
                            {
                                FileName = file.FileName,
                                SourceId = stream.Id.ToString(),
                                ContentType = file.ContentType,
                                FileLength = stream.Length,
                            };

                            filesToReturn.Add(fileToReturn);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "CourseController/UploadFile");
                    this.TempData[MessageConstant.ErrorMessage] = "A problem occurred while recording";
                }
            }

            return filesToReturn;
        }

        private async Task<FileViewModel> UploadImage(IFormFile file)
        {
            var image = new FileViewModel();
            try
            {
                if (file != null && file.Length > 0)
                {
                    using MemoryStream memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    var source = memoryStream.ToArray();
                    var id = await this.bucket.UploadFromBytesAsync(file.FileName, source);

                    image.FileName = file.FileName;
                    image.SourceId = id.ToString();
                    image.ContentType = file.ContentType;
                    image.FileLength = file.Length;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "CourseController/UploadFile");
                this.TempData[MessageConstant.ErrorMessage] = "A problem occurred while recording";
            }

            return image;
        }
    }
}

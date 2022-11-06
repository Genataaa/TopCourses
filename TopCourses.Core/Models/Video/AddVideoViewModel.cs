﻿namespace TopCourses.Core.Models.Video
{
    using System.ComponentModel.DataAnnotations;
    using TopCourses.Core.Models.Section;

    public class AddVideoViewModel
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Title { get; set; } = null!;

        [Url]
        [Required]
        public string VideoUrl { get; set; } = null!;

        public int SectionId { get; set; }
        public SectionModel Section { get; set; }
    }
}
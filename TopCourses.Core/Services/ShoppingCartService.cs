﻿namespace TopCourses.Core.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using TopCourses.Core.Contracts;
    using TopCourses.Core.Data.Common;
    using TopCourses.Core.Models.ShoppingCart;
    using TopCourses.Infrastructure.Data.Identity;
    using TopCourses.Infrastructure.Data.Models;

    public class ShoppingCartService : IShoppingCartService
    {
        private readonly IRepository repository;

        public ShoppingCartService(IRepository repository)
        {
            this.repository = repository;
        }

        public async Task AddCourseInShoppingCart(int courseId, string userId)
        {
            var course = await this.repository.GetByIdAsync<Course>(courseId);
            var user = await this.repository.GetByIdAsync<ApplicationUser>(userId);

            if (course == null || user == null)
            {
                return;
            }

            if (user.ShoppingCartId == null)
            {
                var shoppingCart = new ShoppingCart()
                {
                    UserId = user.Id
                };

                await this.repository.AddAsync(shoppingCart);
                await this.repository.SaveChangesAsync();

                user.ShoppingCartId = shoppingCart.Id;
            }

            user.ShoppingCart.ShoppingCartCourses.Add(course);
            await this.repository.SaveChangesAsync();
        }

        public async Task DeleteAllCoursesFromShoppingCart(string userId)
        {
            var user = await this.repository.GetByIdAsync<ApplicationUser>(userId);

            if (user == null)
            {
                return;
            }

            user.ShoppingCart.ShoppingCartCourses.Clear();
            await this.repository.SaveChangesAsync();
        }

        public async Task DeleteCourseFromShoppingCart(int courseId, string userId)
        {
            var course = await this.repository.GetByIdAsync<Course>(courseId);
            var user = await this.repository.GetByIdAsync<ApplicationUser>(userId);

            if (course == null || user == null)
            {
                return;
            }

            user.ShoppingCart.ShoppingCartCourses.Remove(course);
            await this.repository.SaveChangesAsync();
        }

        public async Task<IEnumerable<ShoppingCartCourseViewModel>> GetAllShoppingCartCoursess(string userId)
        {
            var user = await this.repository.GetByIdAsync<ApplicationUser>(userId);

            if (user == null)
            {
                return null;
            }

            var cart = await this.repository.GetByIdAsync<ShoppingCart>(user.ShoppingCartId);

            var courses = user.ShoppingCart.ShoppingCartCourses.Select(c => new ShoppingCartCourseViewModel()
            {
                Id = c.Id,
                Name = c.Title,
                CreatorFullName = c.Creator.FirstName + " " + c.Creator.LastName,
                ImageUrl = c.ImageUrl,
                Price = c.Price
            }).ToList();

            return courses;
        }
    }
}

﻿namespace TopCourses.Core.Models.ShoppingCart
{

    public class ShoppingCartCourseViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string CreatorFullName { get; set; }

        public string ImageUrl { get; set; }

        public decimal Price { get; set; }
    }
}

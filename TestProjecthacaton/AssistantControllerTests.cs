using Hacaton.Controllers;
using Hacaton.Data;
using Hacaton.Models;
using Hacaton.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;


namespace TestProjecthacaton
{
    [TestFixture]
    internal class AssistantControllerTests
    {
        private ApplicationDbContext _context = null!;
        private Mock<IProductRecommendationService> _recommendationServiceMock = null!;
        private AssistantController _controller = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _recommendationServiceMock =
                new Mock<IProductRecommendationService>();

            _controller = new AssistantController(
                _context,
                _recommendationServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        [Test]
        public async Task GetProducts_ReturnsOnlyProductsInStock()
        {
            // Arrange
            _context.Products.AddRange(
                new Product
                {
                    Id = 1,
                    Name = "Espresso",
                    Price = 50,
                    Category = "Coffee",
                    ImageUrl = "espresso.jpg",
                    InStock = true
                },
                new Product
                {
                    Id = 2,
                    Name = "Latte",
                    Price = 70,
                    Category = "Coffee",
                    ImageUrl = "latte.jpg",
                    InStock = false
                }
            );

            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetProducts();

            // Assert
            Assert.That(
                result.Result,
                Is.TypeOf<OkObjectResult>());

            var okResult = result.Result as OkObjectResult;

            var products = okResult!.Value as List<ProductSummaryDto>;

            Assert.That(products, Is.Not.Null);
            Assert.That(products!.Count, Is.EqualTo(1));

            Assert.That(products[0].Id, Is.EqualTo(1));
            Assert.That(products[0].Name, Is.EqualTo("Espresso"));
        }
        [Test]
        public async Task GetProducts_ReturnsCorrectProductDto()
        {
            // Arrange
            var product = new Product
            {
                Id = 10,
                Name = "Cappuccino",
                Price = 85,
                Category = "Coffee",
                ImageUrl = "cappuccino.jpg",
                InStock = true
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetProducts();

            // Assert
            var okResult = result.Result as OkObjectResult;

            Assert.That(okResult, Is.Not.Null);

            var products = okResult!.Value as List<ProductSummaryDto>;

            Assert.That(products, Has.Count.EqualTo(1));

            var dto = products![0];

            Assert.That(dto.Id, Is.EqualTo(10));
            Assert.That(dto.Name, Is.EqualTo("Cappuccino"));
            Assert.That(dto.Price, Is.EqualTo(85));
            Assert.That(dto.Category, Is.EqualTo("Coffee"));
            Assert.That(dto.ImageUrl, Is.EqualTo("cappuccino.jpg"));
        }
    }
}
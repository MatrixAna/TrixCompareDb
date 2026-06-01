
using Microsoft.AspNetCore.Mvc;
using TrixCompareDb.Data;
using Microsoft.EntityFrameworkCore;

namespace TrixCompareDb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var products = await _context.Products.ToListAsync();
            return Ok(products);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using TrixCompareDb.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TrixCompareDb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablesController : ControllerBase
    {
        private readonly TableRepository _repo;
        public TablesController(TableRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Gets list of tables from a database using legacy connection string lookup.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string database)
        {
            if (string.IsNullOrEmpty(database))
                return BadRequest("Query parameter 'database' is required.");

            var tables = await _repo.GetTablesAsync(database);
            return Ok(tables);
        }

        /// <summary>
        /// Gets list of tables from a database using Azure AD Interactive authentication.
        /// </summary>
        [HttpPost("list-azure")]
        public async Task<IActionResult> GetTablesAzureAD([FromBody] GetTablesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Server))
                    return BadRequest(new { error = "Server is required." });
                if (string.IsNullOrEmpty(request.Email))
                    return BadRequest(new { error = "Email is required." });
                if (string.IsNullOrEmpty(request.DatabaseName))
                    return BadRequest(new { error = "DatabaseName is required." });

                var tables = await _repo.GetTablesWithAzureADAsync(
                    request.Server,
                    request.DatabaseName,
                    request.Email);

                return Ok(tables);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (OperationCanceledException ex)
            {
                return StatusCode(408, new { error = "Authentication request timed out or was cancelled." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to retrieve tables: {ex.Message}" });
            }
        }
    }

    public class GetTablesRequest
    {
        public string Server { get; set; }
        public string Email { get; set; }
        public string DatabaseName { get; set; }
    }
}

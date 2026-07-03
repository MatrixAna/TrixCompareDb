using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using TrixCompareDb.Data;
using TrixCompareDb.Models;
using TrixCompareDb.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TrixCompareDb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompareController : ControllerBase
    {
        private readonly TableRepository _repo;
        private readonly CompareTables _comparer;
        private readonly UpdateTableService _updateService;

        public CompareController(TableRepository repo, CompareTables comparer, UpdateTableService updateService)
        {
            _repo = repo;
            _comparer = comparer;
            _updateService = updateService;
        }

        [HttpPost]
        public async Task<IActionResult> Compare([FromBody] CompareRequest request)
        {
            // Compare the same table name between two different connection strings / databases
            var source = await _repo.GetTable(request.DatabaseSource, request.TableName);
            var target = await _repo.GetTable(request.DatabaseTarget, request.TableName);

            var result = _comparer.Compare(source, target);

            return Ok(result);
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CompareRequest request)
        {
            // Validate request
            if (string.IsNullOrEmpty(request.DatabaseSource) || 
                string.IsNullOrEmpty(request.DatabaseTarget) || 
                string.IsNullOrEmpty(request.TableName))
            {
                return BadRequest("DatabaseSource, DatabaseTarget, and TableName are required.");
            }

            var result = await _updateService.UpdateTargetTableAsync(
                request.DatabaseSource,
                request.DatabaseTarget,
                request.TableName);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return StatusCode(500, result);
            }
        }
    }
}

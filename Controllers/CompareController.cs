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
            try
            {
                // Check if using MFA-based authentication (new way) or legacy connection strings
                bool useMfa = !string.IsNullOrEmpty(request.SourceServer) && 
                             !string.IsNullOrEmpty(request.SourceEmail) &&
                             !string.IsNullOrEmpty(request.TargetServer) && 
                             !string.IsNullOrEmpty(request.TargetEmail);

                List<Dictionary<string, object>> source;
                List<Dictionary<string, object>> target;

                if (useMfa)
                {
                    // Extract database name from table name if it contains schema (e.g., "dbo.Products" -> "Products")
                    // For MFA, we need to infer or use a default database name
                    // If tableName is "dbo.Products", the database name should come from context
                    // For now, use a parameter or ask user to provide it in future

                    // Temporary: assume database name matches or is part of connection context
                    // In production, add databaseName to request model
                    string sourceDb = "TrixCompareDb";  // Default - should be configurable
                    string targetDb = "TrixCompareDb";

                    source = await _repo.GetTableWithAzureADAsync(
                        request.SourceServer,
                        sourceDb,
                        request.SourceEmail,
                        request.TableName);

                    target = await _repo.GetTableWithAzureADAsync(
                        request.TargetServer,
                        targetDb,
                        request.TargetEmail,
                        request.TableName);
                }
                else
                {
                    // Legacy method using connection strings from config
                    source = await _repo.GetTable(request.DatabaseSource, request.TableName);
                    target = await _repo.GetTable(request.DatabaseTarget, request.TableName);
                }

                var result = _comparer.Compare(source, target);
                return Ok(result);
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
                return StatusCode(500, new { error = $"Comparison failed: {ex.Message}" });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] CompareRequest request)
        {
            try
            {
                // Validate request - need either legacy or MFA fields
                bool useMfa = !string.IsNullOrEmpty(request.SourceServer) && 
                             !string.IsNullOrEmpty(request.SourceEmail) &&
                             !string.IsNullOrEmpty(request.TargetServer) && 
                             !string.IsNullOrEmpty(request.TargetEmail);

                bool useLegacy = !string.IsNullOrEmpty(request.DatabaseSource) && 
                                !string.IsNullOrEmpty(request.DatabaseTarget);

                if (!useMfa && !useLegacy)
                {
                    return BadRequest(new { error = "Either provide MFA credentials (SourceServer, SourceEmail, TargetServer, TargetEmail) or legacy database names (DatabaseSource, DatabaseTarget)." });
                }

                if (string.IsNullOrEmpty(request.TableName))
                {
                    return BadRequest(new { error = "TableName is required." });
                }

                UpdateResult result;

                if (useMfa)
                {
                    // Use Azure AD authentication
                    string sourceDb = "TrixCompareDb";  // Default - should be configurable
                    string targetDb = "TrixCompareDb";

                    result = await _updateService.UpdateTargetTableWithAzureADAsync(
                        request.SourceServer,
                        sourceDb,
                        request.SourceEmail,
                        request.TargetServer,
                        targetDb,
                        request.TargetEmail,
                        request.TableName);
                }
                else
                {
                    // Use legacy connection strings
                    result = await _updateService.UpdateTargetTableAsync(
                        request.DatabaseSource,
                        request.DatabaseTarget,
                        request.TableName);
                }

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, result);
                }
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
                return StatusCode(500, new { error = $"Update failed: {ex.Message}" });
            }
        }
    }
}

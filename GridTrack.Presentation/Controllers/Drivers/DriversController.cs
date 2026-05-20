using GridTrack.Presentation.Abstractions.Api;
using Microsoft.AspNetCore.Mvc;

namespace GridTrack.Presentation.Controllers.Drivers;

    [ApiController]
    [Route("api/drivers")]
    public class DriversController : ControllerBase
    {
        // GET: api/drivers
        [HttpGet]
        public async Task<ActionResult<PagedResponse<DriverListItemResponse>>> GetDrivers(
            [FromQuery] GetDriversRequest request)
        {
            // Implementation for paginated list of drivers with filtering
            // This would typically call into your application layer
            throw new NotImplementedException();
        }

        // PATCH: api/drivers/{id}/availability
        [HttpPatch("{id}/availability")]
        public async Task<ActionResult<DriverAvailabilityResponse>> UpdateDriverAvailability(
            [FromRoute] string id,
            [FromBody] UpdateDriverAvailabilityRequest request)
        {
            // Implementation for updating driver availability status
            // This would typically call into your application layer
            throw new NotImplementedException();
        }
    }

    
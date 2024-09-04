using Microsoft.AspNetCore.Mvc;
using RabbitMq;
using RabbitMQ_Api.Consumer;

namespace RabbitMQ_Api.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConsumerController : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            await Task.CompletedTask;
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromServices] PublisherConsumer publisher, [FromBody] ConsumerModel body)
        {
            try
            {
                await publisher.Publish(body, null, CancellationToken.None);
                return Ok();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}

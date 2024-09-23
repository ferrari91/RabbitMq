using Microsoft.AspNetCore.Mvc;
using RabbitMQ_Api.Model;
using RabbitMQ_Api.Publisher;
using RabbitMQ_Api.Statics;

namespace RabbitMQ_Api.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConsumerController : ControllerBase
    {
        [HttpPost("publisher")]
        public async Task Post([FromServices] IMyModelPublisher<MyModel> publisher, [FromBody] string name) => await publisher.Publish(new MyModel(name), null, CancellationToken.None);

        [HttpPut("change-flag")]
        public void Put([FromQuery] bool flag) => Define.ShouldThrow = flag;
    }
}

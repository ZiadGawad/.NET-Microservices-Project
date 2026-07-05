
using System.Text;
using System.Threading.Tasks;
using CommandsService.EventProcessing;
using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CommandsService.AysncDataServices
{
    public class MessageBusSubscriber : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IEventProcessor _eventProcessor;
        private IConnection _connection;
        private IChannel _channel;
        private string _queueName;

        public MessageBusSubscriber(
            IConfiguration configuration, 
            IEventProcessor eventProcesser)
        {
            _configuration = configuration;
            _eventProcessor = eventProcesser;

            InitializeRabbitMQ().GetAwaiter().GetResult();
        }

        private async Task InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory() { HostName = _configuration["RabbitMQHost"], Port = int.Parse(_configuration["RabbitMQPort"]) };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            _channel.ExchangeDeclareAsync(exchange: "trigger", ExchangeType.Fanout);
            _queueName = _channel.QueueDeclareAsync().Result.QueueName;
            _channel.QueueBindAsync(queue: _queueName,
                exchange: "trigger",
                routingKey: "");
            Console.WriteLine("--> Listening On The Message Bus...");

            _connection.ConnectionShutdownAsync += RabbitMQ_ConnectionShutdown;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (ModuleHandle, ea) =>
            {
                Console.WriteLine("--> Event Recieved!");

                var body = ea.Body;
                var notificationMessage = Encoding.UTF8.GetString(body.ToArray());

                _eventProcessor.ProcessEvent(notificationMessage);

                await Task.CompletedTask;
            };

            _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);

            return Task.CompletedTask;
        }

        private async Task RabbitMQ_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Console.WriteLine("--> Connection Shutdown");
        }

        public override void Dispose()
        {
            if (_channel != null && _channel.IsOpen)
            {
                _channel.CloseAsync();
                _connection.CloseAsync();
            }

            base.Dispose();
        }
    }
}

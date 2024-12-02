using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GettingStarted
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var x = CreateHostBuilder(args).Build();
            await x.StartAsync();

            await Task.Delay(1000);
            await x.Services.GetRequiredService<IBus>().Publish(new Ping { Value = "test" });
            await Task.Delay(5000);
            await x.StopAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
	                services.AddDbContext<TestDbContext>();
                    services.AddMassTransit(x =>
                    {
	                    x.AddConsumer<PingConsumer>();
	                    x.AddConsumer<PongConsumer>();
	                    x.AddPublishObserver<TestObserver>();
	                    x.AddConsumeObserver<TestObserver>();

						x.AddEntityFrameworkOutbox<TestDbContext>(o =>
						{
							o.UseSqlServer().UseBusOutbox();
						});

						x.UsingRabbitMq((context, cfg) =>
                        {
	                        cfg.Host("amqp://user:password@localhost:5672/");
							cfg.ReceiveEndpoint("test", ep =>
							{
								ep.Durable = true;
								ep.ConfigureConsumers(context);
								ep.UseEntityFrameworkOutbox<TestDbContext>(context);
							});
							cfg.ConfigureEndpoints(context);
                        });
                    });
                });
    }

    public record Ping
    {
	    public string Value { get; init; }
    } 
    public record Pong
    {
	    public string Value { get; init; }
    }

    public class PingConsumer :
	    IConsumer<Ping>
    {
	    private readonly ILogger<PingConsumer> _logger;

	    public PingConsumer(ILogger<PingConsumer> logger)
	    {
		    _logger = logger;
	    }

	    public async Task Consume(ConsumeContext<Ping> context)
	    {
		    _logger.LogInformation("Ping received Text: {Text}", context.Message.Value);
		    await context.Publish(new Pong { Value = $"responding to {context.Message.Value}" });
		    await context.GetServiceOrCreateInstance<TestDbContext>().SaveChangesAsync();
	    }
    } 
    public class PongConsumer :
	    IConsumer<Pong>
    {
	    private readonly ILogger<PongConsumer> _logger;

	    public PongConsumer(ILogger<PongConsumer> logger)
	    {
		    _logger = logger;
	    }

	    public Task Consume(ConsumeContext<Pong> context)
	    {
		    _logger.LogInformation("Pong received Text: {Text}", context.Message.Value);
		    return Task.CompletedTask;
	    }
    }

    public class TestObserver : IPublishObserver, IConsumeObserver
    {
	    readonly ILogger<TestObserver> _logger;

	    public TestObserver(ILogger<TestObserver> logger)
	    {
		    _logger = logger;
	    }

	    public Task PrePublish<T>(PublishContext<T> context) where T : class
	    {
		    return Task.CompletedTask;
	    }

	    public Task PostPublish<T>(PublishContext<T> context) where T : class
	    {
		    _logger.LogInformation($"Publish observer called for {context.Message.GetType()}");
            return Task.CompletedTask;
	    }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class
	    {
		    return Task.CompletedTask;
	    }


	    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
	    {
		    return Task.CompletedTask;
	    }

        public Task PostConsume<T>(ConsumeContext<T> context) where T : class
	    {
		    _logger.LogInformation($"Consume observer called for {context.Message.GetType()}");
		    return Task.CompletedTask;
	    }

        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
	    {
		    return Task.CompletedTask;
	    }
    }

    public class TestDbContext : DbContext
    {
	    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	    {
		    optionsBuilder.UseSqlServer(connectionString: "Data Source=localhost;Initial Catalog=Test");

            base.OnConfiguring(optionsBuilder);
	    }

	    protected override void OnModelCreating(ModelBuilder modelBuilder)
	    {
		    base.OnModelCreating(modelBuilder);

		    modelBuilder.AddInboxStateEntity();
		    modelBuilder.AddOutboxMessageEntity();
		    modelBuilder.AddOutboxStateEntity();
	    }
    }
}

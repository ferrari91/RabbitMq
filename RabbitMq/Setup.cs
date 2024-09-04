using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace RabbitMq
{
    public static class Setup
    {
        public const string Host = nameof(Host);
        public const string VHost = nameof(VHost);
        public const string Port = nameof(Port);
        public const string User = nameof(User);
        public const string Password = nameof(Password);
        public const string SSL = nameof(SSL);

        public static IServiceCollection AddRabbitMq(this IServiceCollection services, string hostname, string virtualHost, int port, string user, string password, bool ssl)
        {
            services.AddSingleton<IConnectionFactory>(CreateConnection(hostname, virtualHost, port, user, password, ssl));
            services.AddSingleton<Connection>();
            return services;
        }

        private static ConnectionFactory CreateConnection(string hostname, string virtualHost, int port, string user, string passWord, bool ssl)
        {
            var connectionFactory = new ConnectionFactory()
            {
                HostName = hostname,
                VirtualHost = virtualHost,
                Port = port,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                RequestedChannelMax = 2048,
                UserName = user,
                Password = passWord,
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };

            if (ssl)
            {
                connectionFactory.Ssl = new SslOption()
                {
                    ServerName = hostname,
                    Enabled = true
                };
            }

            return connectionFactory;
        }
    }
}

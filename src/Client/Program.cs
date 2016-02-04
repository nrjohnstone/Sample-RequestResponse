using System.Threading;

namespace Client
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using log4net.Config;
    using MassTransit;
    using MassTransit.Log4NetIntegration.Logging;
    using Sample.MessageTypes;


    class Program
    {
        static void Main()
        {
            ConfigureLogger();

            // MassTransit to use Log4Net
            Log4NetLogger.Use();

            IBusControl busControl = CreateBus();

            busControl.Start();

            try
            {
                IRequestClient<ISimpleRequest, ISimpleResponse> client = CreateRequestClient(busControl);

                for (;;)
                {
                    Console.Write("Enter customer id (quit exits): ");
                    string customerId = Console.ReadLine();
                    if (customerId == "quit")
                        break;

                    Console.WriteLine($"Main thread {Thread.CurrentThread.ManagedThreadId}");
                    // this is run as a Task to avoid weird console application issues
                    //var t1 = Task.Run(async () =>
                    //{
                    //    await GetCustomerName(client, customerId);
                    //});

                    //var t2 = Task.Run(async () =>
                    //{
                    //    await GetCustomerName(client, customerId);
                    //});
                    var mainTask = Task.Run(async () =>
                    {
                        Task<string> t1 = GetCustomerName(client, customerId);
                        Task<string> t2 = GetCustomerName(client, customerId);

                        string r1 = await t1;
                        Console.WriteLine("async task 1 returned");
                        string r2 = await t2;
                        Console.WriteLine("async task 2 returned");

                        //string r1 = await GetCustomerName(client, customerId);
                        //string r2 = await GetCustomerName(client, customerId);

                        Console.WriteLine("Customer Name: {0}", r1);
                        Console.WriteLine("Customer Name: {0}", r2);
                    });

                    Console.WriteLine("Doing some other stuff...");
                    mainTask.Wait();
                    Console.WriteLine("all responses received");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception!!! OMG!!! {0}", ex);
            }
            finally
            {
                busControl.Stop();
            }
        }


        private static async Task<string> GetCustomerName(IRequestClient<ISimpleRequest, ISimpleResponse> client, string customerId)
        {
            Console.WriteLine($"async call on thread {Thread.CurrentThread.ManagedThreadId}");
            ISimpleResponse response = await client.Request(new SimpleRequest(customerId));
            Console.WriteLine("client response received");
            return response.CusomerName;
            
        }


        static IRequestClient<ISimpleRequest, ISimpleResponse> CreateRequestClient(IBusControl busControl)
        {
            var serviceAddress = new Uri(ConfigurationManager.AppSettings["ServiceAddress"]);
            IRequestClient<ISimpleRequest, ISimpleResponse> client =
                busControl.CreateRequestClient<ISimpleRequest, ISimpleResponse>(serviceAddress, TimeSpan.FromSeconds(10));

            return client;
        }

        static IBusControl CreateBus()
        {
            return Bus.Factory.CreateUsingRabbitMq(x => x.Host(new Uri(ConfigurationManager.AppSettings["RabbitMQHost"]), h =>
            {
                h.Username("guest");
                h.Password("guest");
            }));
        }

        static void ConfigureLogger()
        {
            const string logConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<log4net>
  <root>
    <level value=""INFO"" />
    <appender-ref ref=""console"" />
  </root>
  <appender name=""console"" type=""log4net.Appender.ColoredConsoleAppender"">
    <layout type=""log4net.Layout.PatternLayout"">
      <conversionPattern value=""%m%n"" />
    </layout>
  </appender>
</log4net>";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(logConfig)))
            {
                XmlConfigurator.Configure(stream);
            }
        }


        class SimpleRequest :
            ISimpleRequest
        {
            readonly string _customerId;
            readonly DateTime _timestamp;

            public SimpleRequest(string customerId)
            {
                _customerId = customerId;
                _timestamp = DateTime.UtcNow;
            }

            public DateTime Timestamp
            {
                get { return _timestamp; }
            }

            public string CustomerId
            {
                get { return _customerId; }
            }
        }
    }
}
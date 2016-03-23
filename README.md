# Griffin.Networking.NetCore
.NET Core (UWP) port of [Griffin.Networking](https://github.com/jgauffin/griffin.networking)

## Example HTTP listener

	internal class Program
	{
		public static void RunDemo()
		{
			var server = new MessagingServer(new MyHttpServiceFactory(),
												new MessagingServerConfiguration(new HttpMessageFactory()));
			server.Start(new IPEndPoint(IPAddress.Loopback, 8888));
		}
	}
	 
	// factory
	public class MyHttpServiceFactory : IServiceFactory
	{
		public IServerService CreateClient(EndPoint remoteEndPoint)
		{
			return new MyHttpService();
		}
	}
	 
	// and the handler
	public class MyHttpService : HttpService
	{
		private static readonly BufferSliceStack Stack = new BufferSliceStack(50, 32000);
	 
		public MyHttpService()
			: base(Stack)
		{
		}
	 
		public override void Dispose()
		{
		}
	 
		public override void OnRequest(IRequest request)
		{
			var response = request.CreateResponse(HttpStatusCode.OK, "Welcome");
	 
			response.Body = new MemoryStream();
			response.ContentType = "text/plain";
			var buffer = Encoding.UTF8.GetBytes("Hello world");
			response.Body.Write(buffer, 0, buffer.Length);
			response.Body.Position = 0;
	 
			Send(response);
		}
	}

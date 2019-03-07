using NetMQ;
using NetMQ.Sockets;

namespace SAFE.Data.Client
{
    // NamedPipes ACL not working in netcore, see https://github.com/dotnet/corefx/issues/31190
    class InterProcessCom
    {
        public static string ReceiveAuthResponse()
        {
            // Start websocket server and listen for message
            using (var server = new ResponseSocket("@tcp://localhost:5556"))
            {
                var authResponse = server.ReceiveFrameString();
                server.SendFrame("Auth response received.");
                return authResponse;
            }
        }

        public static string SendAuthResponse(string authResponse)
        {
            using (var client = new RequestSocket("tcp://localhost:5556"))
            {
                // very important to wait for the ack, otherwise we'll be running through this block too fast,
                // and the first sent message will actually not be picked up by recipient, and auth process will never complete.
                client.SendFrame(authResponse);
                var ack = client.ReceiveFrameString();
                return ack;
            }
        }
    }
}
// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

const int DGRAM_DATA_SIZE = 32768;

string IP = "127.0.0.1";
int tcpPort = 44445;
int udpPort;
string path = ".";
string filename;
string data = ""; 
byte[] buffer = new byte[DGRAM_DATA_SIZE];
List<byte> fileData = new List<byte>();


// Обработка параметров командной строки 
// Command line parameters processing
//try
//{
//    IP = args[0];
//    tcpPort = Int32.Parse(args[1]);
//    path = args[2];
//}
//catch
//{
//    Console.WriteLine("For the correct operation of the program\n" +
//                    "the following arguments must be entered:\n" +
//                    "1. IP\n" +
//                    "2. TCP port\n" +
//                    "3. File path\n" +
//                    "Execution aborted");
//    return -1;
//}


TcpListener server = new TcpListener(IPAddress.Parse(IP), tcpPort);
server.Start();

while (true)
{
    Console.WriteLine("Waiting for connection");
    var tcpClient = server.AcceptTcpClient();
    Console.WriteLine("Connection established");
    NetworkStream stream = tcpClient.GetStream();

    //var bytesCount = stream.Read(buffer, 0, buffer.Length);
    //Console.WriteLine(bytesCount);
    int i;

    // Получаем отправленные на сервер данные
    // Loop to receive the data sent by the client.
    i = stream.Read(buffer, 0, buffer.Length);
    data += System.Text.Encoding.UTF8.GetString(buffer, 0, i);

    // парсинг полученных данных (в формате json для удобства)
    filename = JsonSerializer.Deserialize<BaseInfo>(data).filename;
    udpPort = JsonSerializer.Deserialize<BaseInfo>(data).udpPort;

    // Конечная точка и подключение
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
    UdpClient udpClient = new UdpClient(udpPort);
    var file = File.Open(filename, FileMode.Append);
    stream.Write(BitConverter.GetBytes(123));

    while ((buffer = udpClient.Receive(ref endPoint)).Length != 0)
    {
        MarkedDatagram markedOne = JsonSerializer.Deserialize<MarkedDatagram>(Deserialize(buffer));
        //Console.WriteLine(DecodeBase64(markedOne.data));
        Console.WriteLine(markedOne.id);
        stream.Write(BitConverter.GetBytes(markedOne.id));
        //stream.Write(BitConverter.GetBytes(123));
        file.Write(Encoding.UTF8.GetBytes(DecodeBase64(markedOne.data)));
    }
    file.Close();
    try
    {
        

    }
    catch (JsonException)
    {
        Console.WriteLine("Recieved arguments does not match the required ones");
        return -1;
    }
    catch (ArgumentNullException)
    {
        Console.WriteLine("Recieved argument was null");
        return -1;
    }
    catch (SocketException)
    {
        Console.WriteLine("Received combination \"IP/UDP Port\" does not exist");
        return -1;
    }

    return 0;
}

string Deserialize(byte[] data)
{
    return Encoding.UTF8.GetString(data);
}

string DecodeBase64(string value)
{
    var valueBytes = Convert.FromBase64String(value);
    return Encoding.UTF8.GetString(valueBytes);
}

struct MarkedDatagram
{
    public int id { get; set; }
    public string data { get; set; }

    public MarkedDatagram(int id, string data)
    {
        this.id = id;
        this.data = data;
    }
}

struct BaseInfo
{
    public string filename { get; set; }
    public int udpPort { get; set; }

    public BaseInfo(string filename, int udpPort)
    {
        this.filename = filename;
        this.udpPort = udpPort;
    }
}
// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

const int DGRAM_DATA_SIZE = 32768;

string IP = "127.0.0.1";
int tcpPort = 44445;
int udpPort = 44444;
string filename = "megamozg.png";
int timeout = 5000;
// Данные из файла 
// File data 
byte[] data;

//try
//{
//    IP = args[0];
//    tcpPort = Int32.Parse(args[1]);
//    udpPort = Int32.Parse(args[2]);
//    filename = args[3];
//    timeout = Int32.Parse(args[4]);

//    if (!File.Exists(filename))
//    {
//        Console.WriteLine("Specified file do not exists");
//    }

//    // Проверка на опасные символы в пути/названии файла 
//    // Check for dangerous symbols in filename/file path
//    // ? , : " * > < |
//    if (Regex.IsMatch(filename, "(|\\?|\\,|:|\"|\\*|>|<|\\||)"))
//    {
//        Console.WriteLine("Invalid filename or file path");
//    }
//}
//catch
//{
//    Console.WriteLine("For the correct operation of the program\n" +
//                    "the following arguments must be entered:\n" +
//                    "1. IP\n" +
//                    "2. TCP Port\n" +
//                    "3. UDP Port\n" +
//                    "4. Filename\n" +
//                    "5. TCP connection timeout\n" +
//                    "Execution aborted");
//    return -1;
//}



// Установление соединения
// Connection estblishing
TcpClient tcpClient = new TcpClient(IP, tcpPort);
UdpClient udpClient = new UdpClient(IP, udpPort);
BaseInfo baseInfo = new BaseInfo(filename, udpPort);
TimeSpan responeTime = TimeSpan.FromMilliseconds(timeout);
MarkedDatagram markdgram;

if (!tcpClient.Connected)
{
    Console.WriteLine("No connection");
}

var tcpStream = tcpClient.GetStream();
var serializedBaseInfo = Serialize<BaseInfo>(baseInfo);
tcpStream.Write(serializedBaseInfo);
// Отправка файла
// File sending
data = ReadFile(filename);
Console.WriteLine(Math.Floor((double)data.Length / DGRAM_DATA_SIZE));

byte[] serializedDatagram;
byte[] buffer = new byte[DGRAM_DATA_SIZE];
for (int i = 0; i < Math.Floor((double)data.Length / DGRAM_DATA_SIZE) - 1; i++)
{
    markdgram = new MarkedDatagram(i, data
                                        .Skip(i * DGRAM_DATA_SIZE)
                                        .Take(DGRAM_DATA_SIZE)
                                        .ToArray());
    serializedDatagram = Serialize<MarkedDatagram>(markdgram);
    udpClient.Send(serializedDatagram, serializedDatagram.Length);
    //tcpStream.Read(buffer, 0, buffer.Length);
    //Console.WriteLine($"id:{BitConverter.ToInt32(buffer)}");
    //Console.WriteLine(i);
    Task<bool> sendingConfirmation = Task.Run(() => SendingConfirmation(i));
    //tcpStream.Read(buffer, 0, buffer.Length);

    sendingConfirmation.Wait();
    Console.WriteLine(sendingConfirmation.Result);
    if (!sendingConfirmation.Wait(responeTime))
    {
        i--;
        Console.WriteLine(i);
        continue;
    }
    if (!sendingConfirmation.Result)
    {
        i--;
        Console.WriteLine("32123");
        continue;
    }
}

tcpStream.Close();
tcpClient.Close();
return 0;

bool SendingConfirmation(int i)
{
    byte[] buffer = new byte[DGRAM_DATA_SIZE];
    tcpStream.Read(buffer, 0, buffer.Length);
    Console.WriteLine($"id:{BitConverter.ToInt32(buffer)}");
    if (BitConverter.ToInt32(buffer) == i)
        return true;
    else
        return false;
}

byte[] ReadFile(string filename)
{
    return Encoding.UTF8.GetBytes(File.ReadAllText(filename));
}


static byte[] Serialize<T>(T data)
    where T : struct
{
    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize<T>(data));
}

struct MarkedDatagram
{
    public int id { get; set; }
    public byte[]? data { get; set; }

    public MarkedDatagram(int id, byte[] data)
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
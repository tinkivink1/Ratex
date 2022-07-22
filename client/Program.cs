using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;


const int DGRAM_DATA_SIZE = 32768;  // Размер данных, отправленных в датаграмме

string IP;
int tcpPort;
int udpPort;
string filename;
int timeout;

byte[] data; // Для данных, считанных из файла 

try
{
    IP = args[0];
    tcpPort = Int32.Parse(args[1]);
    udpPort = Int32.Parse(args[2]);
    filename = args[3];
    timeout = Int32.Parse(args[4]);

    if (!File.Exists(filename))
    {
        Console.WriteLine("Specified file do not exists");
        return -1;
    }
}
catch
{
    Console.WriteLine("Error: Unrecognized or incomplete command line.\n\n" +
                    "Using:\n" +
                    "\t\tserver [IP] [TCP port] [UDP port] [Filename] [Timeout]\n" +
                    "Execution aborted");
    return -1;
}



// Установление соединения
TcpClient tcpClient = new TcpClient(IP, tcpPort);
UdpClient udpClient = new UdpClient(IP, udpPort);

// Вспомогательные структуры
MarkedDatagram markdgram; // нумерованная датаграмма для отправки на сервер
BaseInfo baseInfo = new BaseInfo(filename, udpPort); // структура "название файла/UDP порт" для отправки на сервер
TimeSpan responeTime = TimeSpan.FromMilliseconds(timeout); // время ожидания подтверждения отправки от сервера

// Проверка подключения по tcp
if (!tcpClient.Connected)
{
    Console.WriteLine("No connection");
    return -1;
}


var tcpStream = tcpClient.GetStream(); // поток для связи с сервером
var serializedBaseInfo = Serialize<BaseInfo>(baseInfo); // приведние структуры в массив байтов для отправки
byte[] serializedDatagram; // представление нумерованной датаграммы в виде массива байтов

// отправка названия файла и порта на сервер
byte[] buffer = new byte[DGRAM_DATA_SIZE];
tcpStream.Write(serializedBaseInfo);
tcpStream.Read(buffer, 0, buffer.Length);

// Запись файла в память
data = File.ReadAllBytes(filename);


// Выполняется для каждого блока данных
for (int i = 0; i < Math.Ceiling((double)data.Length / DGRAM_DATA_SIZE); i++)
{
    // нумерация блока, перевод в байты и отправка на сервер по udp протоколу
    markdgram = new MarkedDatagram(i, data
                                        .Skip(i * DGRAM_DATA_SIZE)
                                        .Take(DGRAM_DATA_SIZE)
                                        .ToArray());
    serializedDatagram = Serialize<MarkedDatagram>(markdgram);
    udpClient.Send(serializedDatagram, serializedDatagram.Length);

    // Ожидание подтверждения $timeout ms
    Task<bool> sendingConfirmation = Task.Run(() => SendingConfirmation(i));
    if (!sendingConfirmation.Wait(responeTime)) // время отправки вышло
    {
        Console.WriteLine($"Package #{i} delivery time out");
        i--;
        continue;
    }
    if (!sendingConfirmation.Result) // Доставлен не тот пакет
    {
        Console.WriteLine($"Wrong package was delivered #{i}");
        i--;
        continue;
    }
}

// отправка на сервер сообщения об окончании передачи, завершение работы
tcpStream.Write(Encoding.UTF8.GetBytes("#end"));
udpClient.Close();
tcpStream.Close();
tcpClient.Close();
Console.WriteLine("Uploading completed successfully");
return 0;

// Подтверждение полученной сервером датаграммы
bool SendingConfirmation(int i)
{
    byte[] buffer = new byte[DGRAM_DATA_SIZE];
    tcpStream.Read(buffer, 0, buffer.Length);
    Console.WriteLine($"Package #{BitConverter.ToInt32(buffer)} has been sent");
    if (BitConverter.ToInt32(buffer) == i)
        return true;
    else
        return false;
}

// Преобразование Структура -> json -> байты
static byte[] Serialize<T>(T data)
    where T : struct
{
    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize<T>(data));
}

// нумерованная датаграмма
struct MarkedDatagram
{
    public int id { get; set; }
    public byte[] data { get; set; }

    public MarkedDatagram(int id, byte[] data)
    {
        this.id = id;
        this.data = data;
    }
}

// Название файла / порт
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

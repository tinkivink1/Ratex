using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

const int DGRAM_DATA_SIZE = 32768; // Размер данных, отправленных в датаграмме

string IP = "127.0.0.1";
int tcpPort = 44445;
int udpPort;
string path = "../";
string filename;
string baseData = ""; // для данных полученных по tcp
byte[] buffer = new byte[DGRAM_DATA_SIZE]; // буфер для udp пакетов
List<MarkedDatagram> recievedDatagrams = new(); // спиок полученных датаграмм
TimeSpan responeTime = TimeSpan.FromMilliseconds(1); // Время для слушалки конца передачи (IsEnd)

// Обработка параметров командной строки 
try
{
    IP = args[0];
    tcpPort = Int32.Parse(args[1]);
    path = args[2];

    if(path.Last() != '/')
    {
        Console.WriteLine("The last character of the path must be '/' ");
        return -1;
    }
    if (!Directory.Exists(path))
    {
        Console.WriteLine("Specified directory does not exist");
        return -1;
    }
}
catch
{
    Console.WriteLine("Error: Unrecognized or incomplete command line.\n\n" +
                    "Using:\n" +
                    "\t\tserver [IP] [TCP port] [Path]\n" +
                    "Execution aborted");
    return -1;
}


// Слушалка tcp подключений
TcpListener server = new TcpListener(IPAddress.Parse(IP), tcpPort);
server.Start();


TcpClient tcpClient = new TcpClient(); // клиент, на который будет отправляться подтверждения получения пакетов
IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0); // конечная точка - для прослушивания udp
UdpClient udpClient = new UdpClient(); // udp клиент

while (true)
{
    try
    {
        // Ожидание подключения
        // и создание клиента и потока
        Console.WriteLine("Waiting for connection");
        tcpClient = server.AcceptTcpClient();
        var tcpStream = tcpClient.GetStream();
        Console.WriteLine("Connection established");

        // Получаем отправленные на сервер данные
        // Loop to receive the data sent by the client.
        int recievedBytesQuantity = 0;
        recievedBytesQuantity = tcpStream.Read(buffer, 0, buffer.Length);
        baseData = Encoding.UTF8.GetString(buffer, 0, recievedBytesQuantity);

        // парсинг полученных данных
        filename = JsonSerializer.Deserialize<BaseInfo>(baseData).filename;
        udpPort = JsonSerializer.Deserialize<BaseInfo>(baseData).udpPort;
        tcpStream.Write(Encoding.UTF8.GetBytes($"Server received filename: {filename} and UDP port: {udpPort}"));

        // Конечная точка и подключение
        endPoint = new IPEndPoint(IPAddress.Any, 0);
        udpClient = new UdpClient(udpPort);

        // Слушалка #end сообщения
        // #end означает конец передачи
        Task<bool> IsEndTask = Task.Run(() => IsEnd(tcpStream));

        // Выполнять пока не получено сообщение об окончании
        while (!IsEndTask.Wait(responeTime))
        {
            buffer = udpClient.Receive(ref endPoint);
            MarkedDatagram markedOne = JsonSerializer.Deserialize<MarkedDatagram>(buffer);
            Console.WriteLine($"Server recieved packet #{markedOne.id}");
            // Проверка: содержится ли в списке датаграмма с таким же id?   
            if (!(recievedDatagrams
                .Where(item => item.id == markedOne.id)
                .Count() != 0))
            {
                Console.WriteLine($"Packet #{markedOne.id} has been written");
                recievedDatagrams.Add(markedOne); // записываем датаграмму в память (список)
            }
            tcpStream.Write(BitConverter.GetBytes(markedOne.id)); // отправляем подтверждение получения пакета на клиент
        }

        // Сортировка датаграмм по id и запись в файл
        recievedDatagrams.Sort();
        WriteData(path + filename, recievedDatagrams);

        // Закрытие потоков и подключений
        udpClient.Close();
        tcpClient.Close();
        tcpStream.Close();
    }
    catch (JsonException) // Данные пришли в неправильном формате
    {
        Console.WriteLine("Recieved arguments does not match the required ones");
        Console.WriteLine(Encoding.UTF8.GetString(buffer));
        return -1;
    }
    catch (SocketException) // Ошибка подключения
    {
        Console.WriteLine("Remote host does not respond");
        return -1;
    }

}

// Ожидание сообщения о конце передачи
bool IsEnd(NetworkStream tcpStream)
{
    byte[] endBuffer = new byte[4];
    while (true)
    {
        tcpStream.Read(endBuffer, 0, 4);
        if (Encoding.UTF8.GetString(endBuffer, 0, 4) == "#end")
            return true;
    }
}

// Запись в файл
void WriteData(string filename, List<MarkedDatagram> datagrams)
{
    FileStream file = File.Open(filename, FileMode.Create);
    foreach (var datagram in datagrams) 
        file.Write(datagram.data);
    file.Close();
}


// нумерованная датаграмма
class MarkedDatagram : IComparable
{
    public int id { get; set; }
    public byte[] data { get; set; }

    public MarkedDatagram(int id, byte[] data)
    {
        this.id = id;
        this.data = data;
    }

    // Сравнение по id
    public int CompareTo(object? obj)
    {
        if (obj is MarkedDatagram datagram)
            return id.CompareTo(datagram.id);
        else
            throw new ArgumentException();
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
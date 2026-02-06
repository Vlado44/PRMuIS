using Parking.Biblioteka;
using Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ParkingServer
{
    internal class Server
    {
        private const int UDP_PORT = 50000;
        private const int TCP_PORT = 50001;

        static void Main(string[] args)
        {
            List<ParkingInfo> parkinzi = UnosParkinga();

            Dictionary<int, ZahtevInfo> zahtevi = new Dictionary<int, ZahtevInfo>();
          

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));
            udpSocket.Blocking = false;

            Socket tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpServer.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), TCP_PORT));
            tcpServer.Listen(10);
            tcpServer.Blocking = false;

            List<Socket> clients = new List<Socket>();

            Console.WriteLine("=== Parking Server ===");
            Console.WriteLine("UDP: 127.0.0.1:" + UDP_PORT);
            Console.WriteLine("TCP: " + "127.0.0.1" + ":" + TCP_PORT);    

            byte[] buffer = new byte[2048];

            try
            {
                while (true)
                {
                    List<Socket> checkRead = new List<Socket>();
                    List<Socket> checkError = new List<Socket>();

                    checkRead.Add(udpSocket);
                    checkError.Add(udpSocket);

                    if (clients.Count < 10)
                        checkRead.Add(tcpServer);
                    checkError.Add(tcpServer);

                    for (int i = 0; i < clients.Count; i++)
                    {
                        checkRead.Add(clients[i]);
                        checkError.Add(clients[i]);
                    }
                    Socket.Select(checkRead, null, checkError, 1000);

                    if (checkError.Count > 0)
                    {
                        for (int i = 0; i < checkError.Count; i++)
                        {
                            Socket s = checkError[i];

                            if (s != udpSocket && s != tcpServer)
                            {
                                Console.WriteLine("[GRESKA] Klijent greska: " + IspisKlijenta(s));
                                UkloniKlijenta(s, clients, zahtevi);
                            }
                        }
                    }

                    if (checkRead.Count > 0)
                    {
                        for (int i = 0; i < checkRead.Count; i++)
                        {
                            Socket s = checkRead[i];

                            // UDP prijava
                            if (s == udpSocket)
                            {
                                UdpPrijava(udpSocket);
                                continue;
                            }
                        }
                    }
                        if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo k = Console.ReadKey(true);
                        if (k.Key == ConsoleKey.Escape)
                            break;
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket Greska: {ex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska: {ex.Message}");
            }

            for (int i = 0; i < clients.Count; i++)
            {
                try 
                {
                    clients[i].Close(); 
                } 
                catch { }
            }
            try 
            { 
               udpSocket.Close(); 
            } 
            catch { }
            
            try 
            { 
                tcpServer.Close(); 
            } 
            catch { }

            Console.WriteLine("Server zavrsio.");
            Console.ReadKey();
        }

        static void UdpPrijava(Socket udpSocket)
        {
            byte[] buf = new byte[1024];
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);

            int bytes = udpSocket.ReceiveFrom(buf, ref ep);
            string msg = Encoding.UTF8.GetString(buf, 0, bytes).Trim();

            if (msg == "HELLO")
            {
                string odgovor = "TCP;" + "127.0.0.1" + ";" + TCP_PORT;
                udpSocket.SendTo(Encoding.UTF8.GetBytes(odgovor), ep);
                Console.WriteLine("[UDP] HELLO od " + ep.ToString() + " -> " + odgovor);
            }
        }

        static List<ParkingInfo> UnosParkinga()
        {
            List<ParkingInfo> list = new List<ParkingInfo>();

            Console.WriteLine("Unesite broj parkinga: ");
            int n = Convert.ToInt32(Console.ReadLine());

            for (int i = 1; i <= n; i++)
            {
                Console.WriteLine("--- Parking " + " ---");
                Console.WriteLine("Ukupno mesta:");
                int ukupno = Convert.ToInt32(Console.ReadLine());

                Console.WriteLine("Zauzeto mesta:");
                int zauzeto = Convert.ToInt32(Console.ReadLine());
                if (zauzeto <0)
                {
                    zauzeto = 0;
                }
                if(zauzeto > ukupno)
                {
                    zauzeto = ukupno;
                }

                Console.WriteLine("Cena po satu:");
                int cena = Convert.ToInt32(Console.ReadLine());

                ParkingInfo p = new ParkingInfo();
                p.Id = i;
                p.Ukupno = ukupno;
                p.Zauzeto = zauzeto;
                p.CenaPoSatu = cena;

                list.Add(p);
            }
            return list;
        }

        static void UkloniKlijenta(Socket c, List<Socket> clients, Dictionary<int, ZahtevInfo> zahtevi)
        {
            // brise zahteve od klijenta
            List<int> ukloni = new List<int>();
            foreach (KeyValuePair<int, ZahtevInfo> kv in zahtevi)
            {
                if (kv.Value.Owner == c)
                    ukloni.Add(kv.Key);
            }
            for (int i = 0; i < ukloni.Count; i++)
                zahtevi.Remove(ukloni[i]);

            try 
            {
                c.Close(); 
            } catch { }
            clients.Remove(c);
        }

        static string IspisKlijenta(Socket s)
        {
            try 
            { 
                return s.RemoteEndPoint.ToString(); 
            }
            catch 
            {
                return "Nepoznato"; 
            }
        }
    }
}

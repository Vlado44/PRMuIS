using Parking.Biblioteka;
using Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
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

            int requestBroj = new Random().Next(10000, 99999);


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

                            if (s == udpSocket)
                            {
                                UdpPrijava(udpSocket);
                                continue;
                            }

                            if (s == tcpServer)
                            {
                                Socket c = tcpServer.Accept();
                                c.Blocking = false;
                                clients.Add(c);

                                Console.WriteLine("[TCP] Povezan klijent: " + IspisKlijenta(c));

                                PosaljiPaket(c, new Paket("STANJE", parkinzi));
                                continue;
                            }

                            // TCP receive
                            int bytes = 0;
                            try
                            {
                                bytes = s.Receive(buffer);
                            }
                            catch (SocketException)
                            {
                                // non-blocking, ignore
                                continue;
                            }

                            if (bytes <= 0)
                            {
                                Console.WriteLine("[TCP] Diskonektovan: " + IspisKlijenta(s));
                                UkloniKlijenta(s, clients, zahtevi);
                                continue;
                            }

                            Paket p = DeserializePaket(buffer, bytes);
                            if (p == null)
                            {
                                PosaljiPaket(s, new Paket("GRESKA", "Neispravna poruka"));
                                continue;
                            }

                            ObradiTcpZahtev(s, p, parkinzi, clients, zahtevi, ref requestBroj);
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

            Console.WriteLine("\n--- STATISTIKA (max vozila u 1h) ---");
            for (int i = 0; i < parkinzi.Count; i++)
            {
                int max = MaxUJednomSatu(parkinzi[i].Dolasci);
                Console.WriteLine("Parking " + parkinzi[i].Id + ": max u 1h = " + max);
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

            Console.WriteLine("Server zavrsava...");
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

        static void ObradiTcpZahtev(
            Socket client,
            Paket paket,
            List<ParkingInfo> parkinzi,
            List<Socket> clients,
            Dictionary<int, ZahtevInfo> zahtevi,
            ref int requestSeed)
        {
            if (paket.Tip == "ZAUZMI")
            {
                Zauzece z = paket.Podaci as Zauzece;
                if (z == null)
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Neispravan ZAUZMI"));
                    return;
                }

                ParkingInfo p = NadjiParking(parkinzi, z.BrojParkinga);
                if (p == null)
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Nevalidan parking"));
                    return;
                }

                if (z.BrojMesta <= 0)
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Broj mesta mora biti > 0"));
                    return;
                }

                if (z.BrojMesta == 1)
                {
                    if (DaLiJePrazno(z.Proizvodjac) || DaLiJePrazno(z.Model) || DaLiJePrazno(z.Boja) || DaLiJePrazno(z.Registracija))
                    {
                        PosaljiPaket(client, new Paket("GRESKA", "Za 1 mesto posalji i podatke o autu"));
                        return;
                    }
                }

                int slobodno = p.Ukupno - p.Zauzeto;
                int stvarno = z.BrojMesta;
                if (stvarno > slobodno) stvarno = slobodno;

                if (stvarno <= 0)
                {
                    PosaljiPaket(client, new Paket("OBAVESTENJE", "Nema slobodnih mesta"));
                    return;
                }

                z.BrojMesta = stvarno;
                p.Zauzeto += stvarno;

                requestSeed++;
                int reqId = requestSeed;

                ZahtevInfo info = new ZahtevInfo();
                info.ZahtevId = reqId;
                info.Zauzece = z;
                info.Start = DateTime.Now;
                info.Owner = client; 

                zahtevi[reqId] = info;

                Console.WriteLine("[ZAUZMI] ReqID=" + reqId + " Parking=" + p.Id +
                                  " Zauzeto Mesta=" + stvarno + " -> " + p.Zauzeto + "/" + p.Ukupno);

                Potvrda potv = new Potvrda();
                potv.RequestId = reqId;
                potv.StvarnoZauzeto = stvarno;
                potv.poruka = "OK";

                PosaljiPaket(client, new Paket("Potvrda", potv));

                PosaljiStanje(clients, parkinzi);

                return;
            }

            if (paket.Tip == "OSLOBADJAM")
            {
                string tekst = paket.Podaci as string;
                if (tekst == null || !tekst.StartsWith("Oslobadjam:"))
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Neispravan format oslobadjanja"));
                    return;
                }

                int reqId;
                if (!IzvuciRequestId(tekst, out reqId))
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Neispravan RequestId"));
                    return;
                }

                if (!zahtevi.ContainsKey(reqId))
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Nepostojeci zahtev"));
                    return;
                }

                ZahtevInfo info = zahtevi[reqId];

                if (info.Owner != client)
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Nemate pravo da oslobodite ovaj zahtev"));
                    return;
                }

                ParkingInfo p =NadjiParking(parkinzi, info.Zauzece.BrojParkinga);
                if (p == null)
                {
                    PosaljiPaket(client, new Paket("GRESKA", "Parking ne postoji"));
                    return;
                }

                p.Zauzeto -= info.Zauzece.BrojMesta;
                if (p.Zauzeto < 0) p.Zauzeto = 0;

                double mins = (DateTime.Now - info.Start).TotalMinutes;
                int sati = (int)Math.Ceiling(mins);
                if (sati < 1) sati = 1;

                int iznos = sati * info.Zauzece.BrojMesta * p.CenaPoSatu;

                zahtevi.Remove(reqId);

                Console.WriteLine("[OSLOBODI] Req=" + reqId + " Parking=" + p.Id +
                                  " -> " + p.Zauzeto + "/" + p.Ukupno + ", racun=" + iznos);

                PosaljiStanje(clients, parkinzi);

                Racun r = new Racun();
                r.RequestId = reqId;
                r.Iznos = iznos;

                PosaljiPaket(client, new Paket("RACUN", r));
                return;
            }
        }

        static List<ParkingInfo> UnosParkinga()
        {
            List<ParkingInfo> list = new List<ParkingInfo>();

            Console.WriteLine("Unesite broj parkinga: ");
            int n = Convert.ToInt32(Console.ReadLine());

            for (int i = 1; i <= n; i++)
            {
                Console.WriteLine("--- Parking " + i + " ---");
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

        static void PosaljiPaket(Socket s, Paket paket)
        {
            try
            {
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, paket);
                    data = ms.ToArray();
                }
                s.Send(data);
            }
            catch (SocketException)
            {

            }
        }

        static Paket DeserializePaket(byte[] buffer, int count)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(buffer, 0, count))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    return bf.Deserialize(ms) as Paket;
                }
            }
            catch
            {
                return null;
            }
        }

        static ParkingInfo NadjiParking(List<ParkingInfo> parkinzi, int id)
        {
            for (int i = 0; i < parkinzi.Count; i++)
            {
                if (parkinzi[i].Id == id)
                    return parkinzi[i];
            }
            return null;
        }

        static bool IzvuciRequestId(string tekst, out int reqId)
        {
            reqId = 0;
            int idx = tekst.IndexOf(':');
            if (idx < 0) return false;

            string part = tekst.Substring(idx + 1).Trim();
            return int.TryParse(part, out reqId);
        }

        static void PosaljiStanje(List<Socket> clients, List<ParkingInfo> parkinzi)
        {
            Paket state = new Paket("STANJE", parkinzi);
            Socket[] niz = clients.ToArray();
            for (int i = 0; i < niz.Length; i++)
            {
                try
                {
                    PosaljiPaket(niz[i], state);
                }
                catch
                {
                }
            }
        }

        static bool DaLiJePrazno(string s)
        {
            return s == null || s.Trim().Length == 0;
        }

        static int MaxUJednomSatu(List<DateTime> times)
        {
            if (times == null || times.Count == 0) return 0;

            times.Sort();

            int i = 0;
            int max = 1;

            for (int j = 0; j < times.Count; j++)
            {
                while (times[j] - times[i] > TimeSpan.FromMinutes(60))
                    i++;

                int cnt = j - i + 1;
                if (cnt > max) max = cnt;
            }

            return max;
        }
    }
}

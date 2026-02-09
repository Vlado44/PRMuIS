using Parking.Biblioteka;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Client
{
    internal class Program
    {
        private const int UDP_PORT = 50000;
        private const int TCP_PORT = 50001;

        static void Main(string[] args)
        {
            string unos = "";
            while (unos != "HELLO")
            {
                Console.WriteLine("Ukucajte 'HELLO' da biste se prijavili na server:");
                unos = Console.ReadLine();
            }

            Socket clientSocketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EndPoint serverEP = new IPEndPoint(IPAddress.Loopback, UDP_PORT);

            byte[] recvBuff = new byte[1024];
            byte[] messageBuff = new byte[1024];
            messageBuff = Encoding.UTF8.GetBytes("HELLO");

            Console.WriteLine("\n=== Parking Client ===");

            try
            {
                clientSocketUDP.SendTo(messageBuff, serverEP);
                Console.WriteLine("[UDP] Zahtev poslat serveru");

                int bytesReceived = clientSocketUDP.ReceiveFrom(recvBuff, ref serverEP);

                string odgovor = Encoding.UTF8.GetString(recvBuff, 0, bytesReceived);
                Console.WriteLine("[UDP] Odgovor servera: " + odgovor);

                if (odgovor.StartsWith("TCP"))
                {
                    string[] delovi = odgovor.Split(';');
                    string tcpIp = delovi[1];
                    int tcpPort = int.Parse(delovi[2]);

                    clientSocketUDP.Close();

                    PokreniTcpKomunikaciju(tcpIp, tcpPort);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket Greska: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
            }

            Console.WriteLine("\nKlijent ugasen. Pritisnite ENTER za kraj.");
            Console.ReadLine();
        }

        static void PokreniTcpKomunikaciju(string ip, int port)
        {
            try
            {
                Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(ip), port);
                tcpSocket.Connect(serverEP);

                Console.WriteLine("[TCP] Povezan na server.");

                object pocetniOdgovor = PrimiOdgovor(tcpSocket);
                if (pocetniOdgovor is Paket pStanje && pStanje.Tip == "STANJE")
                {
                    List<ParkingInfo> parkinzi = pStanje.Podaci as List<ParkingInfo>;
                    Console.WriteLine("\n--- Trenutno stanje parkinga ---");
                    foreach (var park in parkinzi)
                    {
                        Console.WriteLine($"Parking ID: {park.Id} | Slobodno: {park.Ukupno - park.Zauzeto}/{park.Ukupno} | Cena: {park.CenaPoSatu} RSD");
                    }
                }

                Zauzece zahtev = new Zauzece();
                Console.WriteLine("\nUnesite broj parkinga: ");
                zahtev.BrojParkinga = int.Parse(Console.ReadLine());
                Console.WriteLine("Unesite koliko mesta zauzimate: ");
                zahtev.BrojMesta = int.Parse(Console.ReadLine());
                zahtev.VremeZauzimanja = DateTime.Now;

                if (zahtev.BrojMesta == 1)
                {
                    Console.WriteLine("Proizvodjac: ");
                    zahtev.Proizvodjac = Console.ReadLine();
                    Console.WriteLine("Model: ");
                    zahtev.Model = Console.ReadLine();
                    Console.WriteLine("Boja: ");
                    zahtev.Boja = Console.ReadLine();
                    Console.WriteLine("Registracija: ");
                    zahtev.Registracija = Console.ReadLine();
                }

                Paket p = new Paket("ZAUZMI", zahtev);
                PosaljiPaket(tcpSocket, p);
                Console.WriteLine("[INFO] Zahtev poslat. Cekam potvrdu...");

                object odgovorObj = PrimiOdgovor(tcpSocket);
                if (odgovorObj != null && odgovorObj is Paket pOdgovor && pOdgovor.Podaci is Potvrda potvrda)
                {
                    Console.WriteLine("\n--- ODGOVOR SERVERA ---");
                    Console.WriteLine($"Poruka: {potvrda.poruka}");

                    if (potvrda.RequestId > 0)
                    {
                        Console.WriteLine($"Vas jedinstveni ID zahteva je: {potvrda.RequestId}");
                        Console.WriteLine($"Stvarno zauzeto mesta: {potvrda.StvarnoZauzeto}");

                        Console.WriteLine("Pritisnite ENTER kada budete zeleli da oslobodite mesto.");
                        Console.ReadLine();

                        string komandaTekst = $"Oslobadjam:{potvrda.RequestId}";
                        Paket paketOslobodi = new Paket("OSLOBADJAM", komandaTekst);
                        PosaljiPaket(tcpSocket, paketOslobodi);

                        Console.WriteLine("[INFO] Zahtev za oslobadjanje poslat. Cekam racun...");

                        while (true)
                        {
                            object obj = PrimiOdgovor(tcpSocket);
                            if (obj == null)
                            {
                                Console.WriteLine("[GRESKA] Prekinuta veza");
                                break;
                            }

                            Paket pr = obj as Paket;
                            if (pr == null)
                            {
                                Console.WriteLine("[GRESKA] Nije stigao Paket.");
                                break;
                            }

                            if (pr.Tip == "STANJE")
                            {
                                continue;
                            }

                            if (pr.Tip == "RACUN")
                            {
                                Racun racun = pr.Podaci as Racun;
                                if (racun != null)
                                {
                                    Console.WriteLine("\n--- RACUN PRIMLJEN ---");
                                    Console.WriteLine("ID zahteva: " + racun.RequestId);
                                    Console.WriteLine("Ukupan iznos: " + racun.Iznos + " RSD");
                                }
                                else
                                {
                                    Console.WriteLine("[GRESKA] RACUN nije dobar.");
                                }
                                break;
                            }

                            if (pr.Tip == "GRESKA")
                            {
                                Console.WriteLine("[GRESKA] Server: " + (pr.Podaci == null ? "" : pr.Podaci.ToString()));
                                break;
                            }

                            Console.WriteLine("[INFO] Stigla poruka: " + pr.Tip);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[INFO] Komunikacija prekinuta ili je stigao neispravan odgovor.");
                }

                tcpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
            }
        }

        static void PosaljiPaket(Socket s, Paket p)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, p);
                byte[] data = ms.ToArray();
                s.Send(data);
            }
        }

        static object PrimiOdgovor(Socket s)
        {
            byte[] buffer = new byte[4096];
            try
            {
                int read = s.Receive(buffer);

                if (read <= 0)
                {
                    Console.WriteLine("[GRESKA] Server je prekinuo vezu.");
                    return null;
                }

                BinaryFormatter bf = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream(buffer, 0, read))
                {
                    return bf.Deserialize(ms);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GRESKA] Desila se greska pri prijemu." + ex.Message);
                return null;
            }

        }
    }
}
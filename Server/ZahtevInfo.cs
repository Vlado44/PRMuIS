using System;
using System.Net.Sockets;
using Parking.Biblioteka;

namespace Server
{
    public class ZahtevInfo
    {
        public int ZahtevId { get; set; }
        public Zauzece Zauzece { get; set; }
        public DateTime Start {  get; set; }

        //"Vlasnik" zahteva, da ne bi mogli drugi klijenti da brisu "tudje" zahteve
        public Socket Owner { get; set; } 
    }
}

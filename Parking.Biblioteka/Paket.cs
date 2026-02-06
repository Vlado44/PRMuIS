using System;

namespace Parking.Biblioteka
{
    [Serializable]
    public class Paket
    {
        public string Tip { get; set; }
        public object Podaci { get; set; }

        public Paket() { }

        public Paket(string tip, object podaci) 
        {
            Tip = tip;
            Podaci = podaci;
        }
    }
}

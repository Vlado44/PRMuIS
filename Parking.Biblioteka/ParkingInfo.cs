using System;
using System.Collections.Generic;


namespace Parking.Biblioteka
{
    [Serializable]
    public class ParkingInfo
    {
        public int Id { get; set; }
        public int Ukupno { get; set; }
        public int Zauzeto { get; set; }
        public int CenaPoSatu { get; set; }

        //Za Statistiku
        public List<DateTime> Dolasci { get; set; }

        public ParkingInfo() 
        {
            Dolasci = new List<DateTime>();
        }
    }
}

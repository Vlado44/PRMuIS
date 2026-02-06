using System;

namespace Parking.Biblioteka
{
    [Serializable]
    public class Potvrda
    {
        public int RequestId { get; set; }
        public int StvarnoZauzeto { get; set; }
        public string poruka { get; set; }
    }
}

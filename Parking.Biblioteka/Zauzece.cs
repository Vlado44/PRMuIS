using System;

namespace Parking.Biblioteka
{
    [Serializable]
    public class Zauzece
    {
        public int BrojParkinga {  get; set; }
        public int BrojMesta { get; set; }
        public DateTime VremeZauzimanja { get; set; }

        //Samo ako se izabere da se zauzme mesto za 1 automobil
        public string Proizvodjac {  get; set; }
        public string Model {  get; set; }
        public string Boja { get; set; }
        public string Registracija { get; set; }

    }
}

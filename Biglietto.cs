using MFLibEF.Core;
using MFLibEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using PrintClientCore.Business;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace MFLibEF.Business
{
    /// <summary>
    /// 
    /// </summary>
    public class Biglietto : Titolo
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventoId"></param>
        /// <param name="ordinepostoCodice"></param>
        /// <param name="visibilita"></param>
        /// <returns></returns>
        public static List<Codiceriduzione> GetCodiciriduzione(int eventoId, string ordinepostoCodice, Riduzioni.Visibilita visibilita)
        {
            using var db = new MisuratoreContext();
            var evento = db.Eventos.Include(x => x.Classeprezzo.Prezzi).FirstOrDefault(e => e.Id == eventoId);

            var prezzi = evento?.Classeprezzo?.Prezzi?.Where(x => x.OrdinepostoCodice == ordinepostoCodice && x.Enabled) ?? throw new Exception("Prezzi non trovati");

            if (visibilita == Riduzioni.Visibilita.Biglietteria)
                prezzi = prezzi.Where(x => x.Biglietteria);
            if (visibilita == Riduzioni.Visibilita.Online)
                prezzi = prezzi.Where(x => x.Online);

            var codici = prezzi.Select(x => x.RiduzioneCodice).Distinct().ToList();

            return db.Codiceriduziones.Where(cr => codici.Contains(cr.Codice)).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventoId"></param>
        /// <param name="posto"></param>
        /// <param name="postoLabel"></param>
        /// <param name="prezzoId"></param>
        /// <param name="profileTerminaleId"></param>
        /// <param name="profileOperatorId"></param>
        /// <param name="prevendita"></param>
        /// <param name="ivaPreassolta"></param>
        /// <param name="codiceSupporto"></param>
        /// <param name="codiceElettronico"></param>
        /// <param name="codiceAlternativo"></param>
        /// <param name="acquirenteId"></param>
        /// <param name="utilizzatoreId"></param>
        /// <param name="rivenditoreId"></param>
        /// <param name="fila"></param>
        /// <param name="varco"></param>
        /// <param name="prestampa"></param>
        /// <param name="testMode"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static TransazioniLog? EmettiBiglietto(int eventoId, int? posto, string postoLabel, int prezzoId, int profileTerminaleId,
            int profileOperatorId, bool prevendita, string ivaPreassolta, string codiceSupporto, string codiceElettronico, string codiceAlternativo,
            int? acquirenteId, int? utilizzatoreId, int? rivenditoreId, string fila = "", string varco = "", string prestampa = "", bool testMode = false)
        {
            using MisuratoreContext db = new();

            if (!db.Eventos.AsNoTracking().Any(e => e.Id == eventoId))
                throw new ArgumentException("Evento inesistente");

            if (!db.ProfileOperators.AsNoTracking().Any(po => po.Id == profileOperatorId))
                throw new Exception("ProfileOperator non trovato");

            string postoPrefisso = GetPostoPrefisso(eventoId);

            if (!db.Prezzos.AsNoTracking().Any(p => p.Id == prezzoId))
                throw new Exception("Prezzo non trovato");

            ProfileTerminale profileTerminale = db.ProfileTerminales
                .AsNoTracking()
                .SingleOrDefault(pt => pt.Id == profileTerminaleId) ??
                    throw new Exception("Terminale non trovato");

            if (profileTerminale.PuntovenditaId == null)
                throw new Exception("Punto vendita mancante per il terminale");

            int puntoVenditaId = profileTerminale.PuntovenditaId!.Value;

            posto ??= GetNuovoPostoCodiceNoMap();

            NameValueCollection querystring = new()
            {
                ["posto"] = posto!.Value.ToString(CultureInfo.InvariantCulture),
                ["posto_prefisso"] = postoPrefisso,
                ["posto_label"] = Utility.UrlEncode(postoLabel),
                ["specie"] = "B",
                ["prezzo_id"] = prezzoId.ToString(CultureInfo.InvariantCulture),
                ["puntovendita_id"] = puntoVenditaId.ToString(CultureInfo.InvariantCulture),
                ["prevendita"] = prevendita ? "t" : "f",
                ["terminale_id"] = profileTerminaleId.ToString(CultureInfo.InvariantCulture),
                ["operator_id"] = profileOperatorId.ToString(CultureInfo.InvariantCulture),
                ["evento_id"] = eventoId.ToString(CultureInfo.InvariantCulture),
                ["iva_preassolta"] = ivaPreassolta,
                ["codice_supporto"] = codiceSupporto,
                ["codice_elettronico"] = Utility.UrlEncode(codiceElettronico),
                ["codice_alternativo"] = Utility.UrlEncode(codiceAlternativo),
                ["fila"] = Utility.UrlEncode(fila),
                ["varco"] = Utility.UrlEncode(varco),
                ["prestampa"] = Utility.UrlEncode(prestampa),
                ["test_mode"] = testMode ? "t" : "f"
            };

            if (utilizzatoreId != null)
                querystring["persona_id"] = utilizzatoreId!.Value.ToString(CultureInfo.InvariantCulture);

            if (acquirenteId != null)
                querystring["acquirente_id"] = acquirenteId!.Value.ToString(CultureInfo.InvariantCulture);

            if (rivenditoreId != null)
                querystring["rivenditore_id"] = rivenditoreId!.Value.ToString(CultureInfo.InvariantCulture);

            string res = Utility.ChiamaMFServer(querystring, "/titolo/emetti");

            XmlTextReader reader = new(new MemoryStream(Encoding.Default.GetBytes(res)));
            reader.Read();

            if (reader.Name == "error")
                throw new Exception(res.Substring(0, 255));

            if (testMode)
                return null;

            reader.Read();
            int id = Convert.ToInt32(reader.GetAttribute("id"));

            return db.TransazioniLogs
                .AsNoTracking()
                .SingleOrDefault(tl => tl.Id == id);
        }

        /// <summary>
        /// Emette biglietto secondary ticketing.
        /// </summary>
        /// <param name="eventoId"></param>
        /// <param name="posto"></param>
        /// <param name="postoLabel"></param>
        /// <param name="prezzoId"></param>
        /// <param name="profileTerminaleId"></param>
        /// <param name="profileOperatorId"></param>
        /// <param name="prevendita"></param>
        /// <param name="ivaPreassolta"></param>
        /// <param name="codiceSupporto"></param>
        /// <param name="codiceElettronico"></param>
        /// <param name="codiceAlternativo"></param>
        /// <param name="codiceUnivocoNumeroTransazione"></param>
        /// <param name="indirizzoIpTransazione"></param>
        /// <param name="dataOraInizioCheckout"></param>
        /// <param name="dataOraEsecuzionePagamento"></param>
        /// <param name="cro"></param>
        /// <param name="metodoSpedizioneTitolo"></param>
        /// <param name="indirizzoSpedizioneTitolo"></param>
        /// <param name="codiceUnivocoAcquirente"></param>
        /// <param name="indirizzoIpRegistrazione"></param>
        /// <param name="dataOraRegistrazione"></param>
        /// <param name="cellulareAcquirente"></param>
        /// <param name="emailAcquirente"></param>
        /// <param name="autenticazione"></param>
        /// <param name="acquirenteId"></param>
        /// <param name="utilizzatoreId"></param>
        /// <param name="rivenditoreId"></param>
        /// <param name="fila"></param>
        /// <param name="varco"></param>
        /// <param name="prestampa"></param>
        /// <param name="testMode"></param>
        /// <returns></returns>
        /// <exception cref="Exception"/>
        public static TransazioniLog? EmettiBigliettoEsteso(int eventoId, int? posto, string? postoLabel,
            int prezzoId, int profileTerminaleId, int profileOperatorId, bool prevendita, string ivaPreassolta,
            string? codiceSupporto, string? codiceElettronico, string? codiceAlternativo,
            string codiceUnivocoNumeroTransazione, string indirizzoIpTransazione, DateTime dataOraInizioCheckout,
            DateTime? dataOraEsecuzionePagamento, string cro, string metodoSpedizioneTitolo,
            string indirizzoSpedizioneTitolo, string codiceUnivocoAcquirente, string indirizzoIpRegistrazione,
            DateTime dataOraRegistrazione, string cellulareAcquirente, string emailAcquirente,
            string autenticazione, int acquirenteId, int? utilizzatoreId, int? rivenditoreId,
            string fila = "", string varco = "", string prestampa = "", bool testMode = false)
        {
            using MisuratoreContext db = new();

            if (!db.ProfileOperators.AsNoTracking().Any(po => po.Id == profileOperatorId))
                throw new Exception("ProfileOperator non trovato");

            if (!db.Eventos.AsNoTracking().Any(e => e.Id == eventoId))
                throw new Exception("Evento non trovato");

            var profileTerminale = db.ProfileTerminales.AsNoTracking()
                .Where(pt => pt.Id == profileTerminaleId)
                .Select(pt => new { pt.PuntovenditaId })
                .FirstOrDefault() ??
                    throw new Exception("Profile terminale non trovato");

            if (profileTerminale.PuntovenditaId == null)
                throw new Exception("Punto vendita mancante per il terminale");

            if (!db.Puntovenditas.AsNoTracking().Any(pv => pv.Id == profileTerminale.PuntovenditaId.Value))
                throw new Exception("Punto vendita non trovato");

            if (!db.Prezzos.AsNoTracking().Any(p => p.Id == prezzoId))
                throw new Exception("Prezzo non trovato");

            posto ??= GetNuovoPostoCodiceNoMap();

            if (IsEmessoNonBloccato(posto!.Value, eventoId))
                throw new Exception("Il posto ha già un titolo valido");

            string postoPrefisso = GetPostoPrefisso(eventoId);

            if (string.IsNullOrEmpty(codiceUnivocoNumeroTransazione))
                throw new Exception("Codice univoco transazione mancante");

            Persona acquirente = db.Personas.AsNoTracking().FirstOrDefault(p => p.Id == acquirenteId) ??
                throw new Exception("Acquirente non trovato");

            if (string.IsNullOrEmpty(codiceSupporto))
                codiceSupporto = "BT";

            if (!Titolo.VerificaCodiceSupporto(codiceSupporto))
                throw new ArgumentException("Codice supporto non censito");

            if (string.IsNullOrEmpty(acquirente.Nome) || string.IsNullOrEmpty(acquirente.Cognome) || acquirente.DataNascita == null
                || (string.IsNullOrEmpty(acquirente.ComuneNascita) || string.IsNullOrEmpty(acquirente.ProvinciaNascita))
                && string.IsNullOrEmpty(acquirente.NazioneEsteraNascita))
                throw new Exception("Dati acquirente non corretti");

            if (string.IsNullOrEmpty(codiceUnivocoAcquirente))
                throw new Exception("Codice Acquirente mancante");

            if (string.IsNullOrEmpty(indirizzoIpRegistrazione))
                throw new Exception("Indirizzo IP Registrazione Acquirente mancante");

            if (string.IsNullOrEmpty(autenticazione))
                throw new Exception("Autenticazione Acquirente mancante");

            if (string.IsNullOrEmpty(indirizzoIpTransazione) && autenticazione == "OTP")
                throw new Exception("Indirizzo IP transazione mancante");

            //todo univocità posto
            NameValueCollection querystring = new()
            {
                ["posto"] = posto.ToString(),
                ["posto_prefisso"] = postoPrefisso,
                ["posto_label"] = Utility.UrlEncode(postoLabel),
                ["specie"] = "B",
                ["prezzo_id"] = prezzoId.ToString(CultureInfo.InvariantCulture),
                ["puntovendita_id"] = profileTerminale.PuntovenditaId.Value.ToString(CultureInfo.InvariantCulture),
                ["prevendita"] = prevendita ? "t" : "f",
                ["terminale_id"] = profileTerminaleId.ToString(CultureInfo.InvariantCulture),
                ["operator_id"] = profileOperatorId.ToString(CultureInfo.InvariantCulture),
                ["evento_id"] = eventoId.ToString(CultureInfo.InvariantCulture),
                ["iva_preassolta"] = ivaPreassolta,
                ["codice_supporto"] = codiceSupporto,
                ["codice_elettronico"] = Utility.UrlEncode(codiceElettronico),
                ["codice_alternativo"] = Utility.UrlEncode(codiceAlternativo),
                ["fila"] = Utility.UrlEncode(fila),
                ["varco"] = Utility.UrlEncode(varco),
                ["acquirente_id"] = acquirenteId.ToString(CultureInfo.InvariantCulture),
                ["prestampa"] = Utility.UrlEncode(prestampa),
                ["codice_univoco_numero_transazione"] = codiceUnivocoNumeroTransazione,
                ["indirizzo_ip_transazione"] = indirizzoIpTransazione,
                ["data_ora_inizio_checkout"] = dataOraInizioCheckout.ToString("yyyy-MM-dd HH':'mm':'ss"),
                ["cro"] = cro,
                ["metodo_spedizione_titolo"] = metodoSpedizioneTitolo,
                ["indirizzo_spedizione_titolo"] = Utility.UrlEncode(indirizzoSpedizioneTitolo),
                ["codice_univoco_acquirente"] = codiceUnivocoAcquirente,
                ["indirizzo_ip_registrazione"] = indirizzoIpRegistrazione,
                ["data_ora_registrazione"] = dataOraRegistrazione.ToString("yyyy-MM-dd HH':'mm':'ss"),
                ["cellulare_acquirente"] = cellulareAcquirente,
                ["email_acquirente"] = emailAcquirente,
                ["autenticazione"] = autenticazione,
                ["test_mode"] = testMode ? "t" : "f"
            };

            if (utilizzatoreId != null)
                querystring["persona_id"] = ((int)utilizzatoreId).ToString(CultureInfo.InvariantCulture);
            if (rivenditoreId != null)
                querystring["rivenditore_id"] = ((int)rivenditoreId).ToString(CultureInfo.InvariantCulture);
            if (dataOraEsecuzionePagamento != null)
                querystring["data_ora_esecuzione_pagamento"] = ((DateTime)dataOraEsecuzionePagamento).ToString("yyyy-MM-dd HH':'mm':'ss");

            string res = Utility.ChiamaMFServer(querystring, "/titolo/emetti_esteso");

            XmlTextReader reader = new(new MemoryStream(Encoding.Default.GetBytes(res)));

            reader.Read();

            if (reader.Name == "error")
                throw new Exception(res[..255]);

            if (testMode)
                return null;

            reader.Read();

            int id = Convert.ToInt32(reader.GetAttribute("id"));

            return GetTitoloById(id);
        }

        /// <summary>
        /// Genera un PDF per il biglietto specificato. Ritorna null se il biglietto è un annullo o un titolo annullato, o se non è un biglietto.
        /// </summary>
        /// <param name="transazioniLogId"></param>
        /// <param name="background"></param>
        /// <param name="ordinepostoDescrizione"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static byte[] GeneraPdf(
            int transazioniLogId,
            string? background = null,
            string? ordinepostoDescrizione = null)
        {
            using MisuratoreContext db = new();

            TransazioniLog transazioniLog = db.TransazioniLogs.FirstOrDefault(t => t.Id == transazioniLogId) ??
                throw new Exception("TransazioniLog non trovato");

            if (!IsTitoloValido(transazioniLog)) //è un annullo o un titolo annullato.
                throw new Exception("Il titolo risulta annullato oppure è un annullo");
            if (transazioniLog.Specie != "B")
                throw new Exception("Il titolo non è un biglietto");

            Formatotitolo formatotitolo = MfWrapper.GetFormatotitolo(transazioniLog.TerminaleId, "B") ??
                throw new Exception("Formatotitolo non trovato");

            // Se non viene passata come parametro, la recupero in base al codice ordineposto associato al biglietto.
            // Se invece viene passata, la utilizzo (per gestire eventuali personalizzazioni legate alla cessione).
            if (string.IsNullOrEmpty(ordinepostoDescrizione))
            {
                ordinepostoDescrizione = db.Ordinepostos
                    .Where(o => o.Codice == transazioniLog.OrdinepostoCodice)
                    .Select(o => o.Descrizione)
                    .FirstOrDefault() ??
                        throw new Exception("Ordine posto non trovato");
            }

            if (string.IsNullOrEmpty(background))
            {
                var transazioniEvento = db.TransazioniEventis
                    .Include(te => te.Evento)
                    .ThenInclude(e => e.Spettacolo)
                    .FirstOrDefault(e => e.LogId == transazioniLogId);

                background = Background.ElaboraBackgroundBiglietto(
                    formatotitolo.Id,
                    transazioniEvento?.Evento?.Spettacolo?.OrganizzatoreId,
                    transazioniLog.PrezzoId,
                    transazioniEvento?.Evento?.SpettacoloId,
                    transazioniEvento?.Evento?.Id,
                    transazioniEvento?.PostoCodice,
                    transazioniLogId);
            }

            //bool usaModelloAlternativo = !tipoSupporto.Stampabile;
            string formatoTitoloElaborato = ElaboraFormatoTitolo(
                formatotitolo,
                transazioniLog,
                ordinepostoDescrizione,
                string.Empty,
                string.Empty,
                background);

            WriterPdf writerPdf = new(formatoTitoloElaborato);
            byte[] pdfBytes = writerPdf.GeneraPdf();

            if (transazioniLog.CodiceSupporto == "BT")
                RegistraStampa(transazioniLogId, transazioniLog.TerminaleId);

            return pdfBytes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transazioniLogId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static int? GetPosto(int transazioniLogId)
        {
            using var db = new MisuratoreContext();
            if (!db.TransazioniLogs.Any(x => x.Id == transazioniLogId))
                throw new Exception("TransazioniLog non trovata");

            return db.TransazioniEventis.FirstOrDefault(e => e.LogId == transazioniLogId)?.PostoCodice;
        }

        /// <summary>
        /// Ritorna l'evento per il quale il biglietto è stato emesso
        /// </summary>
        /// <param name="bigliettoId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Evento? GetEvento(int bigliettoId)
        {
            using var db = new MisuratoreContext();
            if (!db.TransazioniLogs.Any(x => x.Id == bigliettoId))
                throw new Exception("Biglietto non trovato");

            return db.TransazioniEventis.Include(x => x.Evento).FirstOrDefault(e => e.LogId == bigliettoId)?.Evento;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bigliettoId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static decimal GetTotalePrestazioniComplementari(int bigliettoId)
        {
            using var db = new MisuratoreContext();
            return db.TransazioniLogs.Include(x => x.PrestazioniComplementari).FirstOrDefault(x => x.Id == bigliettoId)?
                .PrestazioniComplementari?.Sum(p => p.Importo) ?? throw new Exception("Biglietto non trovato");
        }

        /// <summary>
        /// Restituisce l'ID dell'utilizzatore del biglietto
        /// </summary>
        /// <param name="transazioniLogId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static int? GetUtilizzatoreId(int transazioniLogId)
        {
            using var db = new MisuratoreContext();

            var transazioniLog = db.TransazioniLogs.FirstOrDefault(x => x.Id == transazioniLogId)
                ?? throw new ArgumentException("TransazioniLog non può essere nullo");

            if (transazioniLog.Specie != "B")
                throw new ArgumentException("TransazioniLog non è un biglietto");

            var cessioneTitolos = db.Cessiones.Include(x => x.Persona).Where(x => x.TransazioniLogId == transazioniLogId).ToList();

            return transazioniLog.PersonaId switch
            {
                not null when cessioneTitolos.Count == 1 => transazioniLog.PersonaId,                                               // biglietto non sponsor e non ceduto
                null when cessioneTitolos.Count == 0 => null,                                                                       // biglietto sponsor non perfezionato
                null when cessioneTitolos.Count == 1 => cessioneTitolos[0].Persona.Id,                                              // biglietto sponsor perfezionato e non ceduto
                _ when cessioneTitolos.Count > 1 => cessioneTitolos.OrderByDescending(c => c.NumeroPassaggio).First().Persona.Id,   // Biglietto (sponsor o non sponsor) ceduto 
                _ => throw new Exception("Caso non contemplato")
            };
        }

        /////// <summary>
        /////// 
        /////// </summary>
        /////// <param name="transazioniLogId"></param>
        /////// <param name="personaId"></param>
        /////// <param name="eventoId"></param>
        /////// <returns></returns>
        /////// <exception cref="ArgumentException"></exception>
        /////// <exception cref="Exception"></exception>
        ////public static bool IsCedibile(int transazioniLogId, int personaId, int eventoId)
        ////{
        ////    if (DomainObjectManager.GetTransazioniLog(transazioniLogId) == null)
        ////        throw new ArgumentException("TransazioniLog non trovato");
        ////    if (DomainObjectManager.GetPersona(personaId) == null)
        ////        throw new ArgumentException("Peronsa non trovata");
        ////    if (DomainObjectManager.GetEvento(eventoId) == null)
        ////        throw new ArgumentException("Evento non trovato");

        ////    var querystring = new NameValueCollection();
        ////    querystring["transazioni_log_id"] = transazioniLogId.ToString(CultureInfo.InvariantCulture);
        ////    querystring["persona_id"] = personaId.ToString(CultureInfo.InvariantCulture);
        ////    querystring["evento_id"] = eventoId.ToString(CultureInfo.InvariantCulture);

        ////    string error;
        ////    string res = Utility.ChiamaMFServer("/cessione/iscedibile", querystring, out error);
        ////    if (!string.IsNullOrEmpty(error))
        ////        throw new Exception(error);

        ////    return res == "1";
        ////}

        private static string ElaboraFormatoTitolo(
            Formatotitolo formatotitolo,
            TransazioniLog transazioniLog,
            string ordinepostoDescrizione,
            string background
        )
        {
            // Estraggo l'ordine posto dal codice del transazioniLog
            OrdinePosto ordineposto = DomainObjectManager.GetOrdinePosto(transazioniLog.OrdinepostoCodice)
                ?? throw new Exception("Ordineposto non trovato");

            // Imposto la descrizione dell'ordine posto usando quella dell'ordine posto solo se non passata come parametro
            ordinePostoDescrizione ??= ordineposto.Descrizione;

            // Sostituisco le variabili nel risultato dell'elaborazione del formato titolo
            return ElaboraFormatoTitoloBase(formatotitolo, transazioniLog, background);
                .Replace("%ordineposto_descrizione%", ordinepostoDescrizione)
                .Replace("%ordineposto_siae%", ordineposto.Descrizione);
        }

        private static string GetPostoPrefisso(int eventoId)
        {
            using MisuratoreContext db = new();

            bool mappaReale = db.MapEventos
                .Where(me => me.EventoId == eventoId)
                .Select(me => me.MappaReale)
                .FirstOrDefault();

            return mappaReale ? "M" : "N";
        }
    }
}

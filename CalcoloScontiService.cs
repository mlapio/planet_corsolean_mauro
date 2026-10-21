


public class CalcoloScontiService
{
    private readonly ITariffaRepository _repo;
    public CalcoloScontiService(ITariffaRepository repo) => _repo = repo;

    public decimal CalcolaPrezzoFinale(int tariffaId, int eta)
    {
        var tariffa = _repo.GetById(tariffaId) 
            ?? throw new KeyNotFoundException("Tariffa inesistente");
            
        if (eta < 18) return tariffa.PrezzoBase * 0.8m; // Sconto 20%
        if (eta >= 65) return tariffa.PrezzoBase * 0.7m; // Sconto 30%
        return tariffa.PrezzoBase;
    }
}



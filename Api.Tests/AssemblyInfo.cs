using Xunit;

// Tous les tests d'intégration partagent la même base SQL Server physique (PoincoTest),
// et chaque classe reset la base dans InitializeAsync (EnsureDeletedAsync + MigrateAsync).
// xUnit parallélise les classes de test par défaut — sans ça, deux classes finissent par
// se marcher dessus (l'une DROP la base pendant que l'autre la CREATE). On force donc
// l'exécution séquentielle pour tout l'assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

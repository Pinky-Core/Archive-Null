namespace ArchiveNull.InvestigationBoard
{
    [System.Serializable]
    public struct BoardConnection
    {
        public string evidenceA;
        public string evidenceB;

        public BoardConnection(string evidenceA, string evidenceB)
        {
            this.evidenceA = evidenceA;
            this.evidenceB = evidenceB;
        }
    }
}

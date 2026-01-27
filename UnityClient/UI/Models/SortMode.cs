namespace OkeyGame.Unity.UI.Models
{
    /// <summary>
    /// Taş sıralama modu.
    /// </summary>
    public enum SortMode
    {
        /// <summary>Sıralama yok (manuel düzen).</summary>
        None = 0,

        /// <summary>Renge göre sırala (sonra değere göre).</summary>
        ByColor = 1,

        /// <summary>Değere göre sırala (sonra renge göre).</summary>
        ByValue = 2
    }
}

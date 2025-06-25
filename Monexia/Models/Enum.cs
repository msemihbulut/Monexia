namespace Monexia.Models
{
    public enum TransactionType
    {
        Income,
        Expense
    }

    public enum IncomeCategory
    {
        Maaş,
        SerbestGelir,
        HisseTemettü,
        KiraGeliri,
        FaizGeliri,
        KriptoKazancı,
        Diğer
    }

    public enum ExpenseCategory
    {
        Gıda,
        Ulaşım,
        Konut,
        Sağlık,
        Eğitim,
        Eğlence,
        Fatura,
        Giyim,
        Tatil,
        KrediKartıÖdemesi,
        Abonelikler,
        Diğer
    }
}
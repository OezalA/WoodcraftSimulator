using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Economy
{
    public enum TransactionKind
    {
        StartingFunds,
        Purchase,
        JobIncome,

        /// <summary>Weekly rent and wages (D-054); may take the balance below zero.</summary>
        RunningCost
    }

    /// <summary>One booking: a positive amount is money in, a negative one money out.</summary>
    public sealed class Transaction
    {
        public Transaction(TransactionKind kind, Money amount, string description)
        {
            Kind = kind;
            Amount = amount;
            Description = description ?? string.Empty;
        }

        public TransactionKind Kind { get; }

        public Money Amount { get; }

        public string Description { get; }
    }

    /// <summary>
    /// The workshop's money (master prompt §6.13): every booking is kept, so expenses, income and purchases can be
    /// shown. A purchase that can't be paid is refused (no credit); only the weekly running costs may take the balance
    /// below zero (debt, D-054: the game goes on, nothing can be bought until jobs pay it back).
    /// </summary>
    public sealed class Ledger
    {
        private readonly List<Transaction> entries = new List<Transaction>();

        public event Action Changed;

        public IReadOnlyList<Transaction> Entries => entries;

        public Money Balance { get; private set; }

        /// <summary>True once starting funds (or a restored history) are in the ledger.</summary>
        public bool IsStarted => entries.Count > 0;

        public Money TotalJobIncome => Sum(TransactionKind.JobIncome);

        /// <summary>Money spent on purchases, as a positive amount.</summary>
        public Money TotalPurchases => -Sum(TransactionKind.Purchase);

        /// <summary>Rent and wages paid, as a positive amount (D-054).</summary>
        public Money TotalRunningCosts => -Sum(TransactionKind.RunningCost);

        public bool IsInDebt => Balance.IsNegative;

        public void Start(Money funds)
        {
            if (IsStarted) throw new InvalidOperationException("The ledger has already been started.");
            if (funds.IsNegative) throw new ArgumentOutOfRangeException(nameof(funds), "Starting funds must not be negative.");

            Book(new Transaction(TransactionKind.StartingFunds, funds, Localization.Loc.T("Starting funds", "Başlangıç parası")));
        }

        public bool CanAfford(Money price) => !price.IsNegative && price <= Balance;

        /// <summary>Pays <paramref name="price"/> if the balance covers it; otherwise nothing changes.</summary>
        public bool TrySpend(Money price, string description)
        {
            if (!CanAfford(price))
            {
                return false;
            }

            Book(new Transaction(TransactionKind.Purchase, -price, description));
            return true;
        }

        public void Earn(Money amount, string description)
        {
            if (amount.IsNegative) throw new ArgumentOutOfRangeException(nameof(amount), "Income must not be negative.");

            Book(new Transaction(TransactionKind.JobIncome, amount, description));
        }

        /// <summary>Books a running cost (rent, wage) whether or not the balance covers it (D-054).</summary>
        public void Charge(Money amount, string description)
        {
            if (amount.IsNegative) throw new ArgumentOutOfRangeException(nameof(amount), "A cost must not be negative.");

            Book(new Transaction(TransactionKind.RunningCost, -amount, description));
        }

        /// <summary>Replaces the history (loading a save); a balance in debt is allowed (D-054).</summary>
        public void Restore(IEnumerable<Transaction> history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));

            var restored = new List<Transaction>(history);
            Money balance = Money.Zero;
            foreach (Transaction entry in restored)
            {
                if (entry == null) throw new ArgumentException("The history contains an empty entry.", nameof(history));
                balance += entry.Amount;
            }

            entries.Clear();
            entries.AddRange(restored);
            Balance = balance;
            Changed?.Invoke();
        }

        private void Book(Transaction entry)
        {
            entries.Add(entry);
            Balance += entry.Amount;
            Changed?.Invoke();
        }

        private Money Sum(TransactionKind kind)
        {
            Money sum = Money.Zero;
            foreach (Transaction entry in entries)
            {
                if (entry.Kind == kind)
                {
                    sum += entry.Amount;
                }
            }

            return sum;
        }
    }
}

using System;
using System.Collections.Generic;

namespace WoodcraftSimulator.Economy
{
    public enum TransactionKind
    {
        StartingFunds,
        Purchase,
        JobIncome
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
    /// shown. The balance never goes below zero: a purchase that can't be paid is refused (no credit).
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

        public void Start(Money funds)
        {
            if (IsStarted) throw new InvalidOperationException("The ledger has already been started.");
            if (funds.IsNegative) throw new ArgumentOutOfRangeException(nameof(funds), "Starting funds must not be negative.");

            Book(new Transaction(TransactionKind.StartingFunds, funds, "Starting funds"));
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

        /// <summary>Replaces the history (loading a save). Throws if it would leave a negative balance.</summary>
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

            if (balance.IsNegative) throw new ArgumentException("The history ends with a negative balance.", nameof(history));

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

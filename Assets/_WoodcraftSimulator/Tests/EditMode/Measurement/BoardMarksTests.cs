using System;
using NUnit.Framework;
using WoodcraftSimulator.Measurement;

namespace WoodcraftSimulator.Tests.Measurement
{
    public sealed class BoardMarksTests
    {
        private static readonly Length Board = Length.FromMillimeters(2400);

        private static Length Mm(double value) => Length.FromMillimeters(value);

        [Test]
        public void DistanceFromReference_FromStart_IsPosition()
        {
            var mark = new PencilMark(Mm(899.6), BoardEnd.Start);
            Assert.That(mark.DistanceFromReference(Board), Is.EqualTo(Mm(899.6)));
        }

        [Test]
        public void DistanceFromReference_FromEnd_IsMeasuredFromTheOtherEnd()
        {
            // 1500.4 mm from the start = 899.6 mm from the end of a 2400 mm board.
            var mark = new PencilMark(Mm(1500.4), BoardEnd.End);
            Assert.That(mark.DistanceFromReference(Board), Is.EqualTo(Mm(899.6)));
        }

        [Test]
        public void Add_StoresMarkAndRaisesEvent()
        {
            var marks = new BoardMarks(Board);
            PencilMark raised = null;
            marks.MarkAdded += m => raised = m;

            var mark = new PencilMark(Mm(900), BoardEnd.Start);
            marks.Add(mark);

            Assert.That(marks.Count, Is.EqualTo(1));
            Assert.That(marks.All[0], Is.SameAs(mark));
            Assert.That(raised, Is.SameAs(mark));
        }

        [TestCase(0.0)]
        [TestCase(2400.0)]
        public void Add_AtTheEnds_IsAllowed(double position)
        {
            var marks = new BoardMarks(Board);
            Assert.DoesNotThrow(() => marks.Add(new PencilMark(Mm(position), BoardEnd.Start)));
        }

        [TestCase(-0.1)]
        [TestCase(2400.1)]
        public void Add_OutsideThePiece_Throws(double position)
        {
            var marks = new BoardMarks(Board);
            Assert.Throws<ArgumentOutOfRangeException>(() => marks.Add(new PencilMark(Mm(position), BoardEnd.Start)));
            Assert.That(marks.Count, Is.Zero);
        }

        [Test]
        public void Add_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BoardMarks(Board).Add(null));
        }

        [Test]
        public void Constructor_NonPositiveLength_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoardMarks(Length.Zero));
        }
    }
}

namespace Memstate.Tests
{
    using System;
    using FakeItEasy;
    using Memstate.Models;
    using Xunit;

    public class EngineOperationalTests
    {
        private readonly MemstateSettings _settings;

        private Command<KeyValueStore<int>> _dummyCommand;
        private FakeSubscriptionSource _fakeSource;
        private DateTime _now;
        private Engine<KeyValueStore<int>> _engine;

        public EngineOperationalTests()
        {
            _settings = new MemstateSettings();
        }

        [Fact]
        public void Broken_sequence_is_not_allowed()
        {
            // Arrange
            _settings.AllowBrokenSequence = false;
            Initialize();

            // apply records with a sequence in the gap
            _fakeSource.RecordHandler.Invoke(new JournalRecord(0, _now, _dummyCommand));
            _fakeSource.RecordHandler.Invoke(new JournalRecord(2, _now, _dummyCommand));

            // engine should now be stopped and throw if transactions are attempted
            Assert.ThrowsAny<Exception>(() => _engine.Execute(_dummyCommand));
        }

        [Fact]
        public void Broken_sequence_is_allowed()
        {
            // Arrange
            _settings.AllowBrokenSequence = true;
            Initialize();

            // apply records with a sequence in the gap
            _fakeSource.RecordHandler.Invoke(new JournalRecord(0, _now, _dummyCommand));
            _fakeSource.RecordHandler.Invoke(new JournalRecord(2, _now, _dummyCommand));

            // engine should now be stopped and throw if transactions are attempted
            _engine.Execute(_dummyCommand);
            Assert.Equal(2, _engine.LastRecordNumber);
        }

        private void Initialize()
        {
            _fakeSource = new FakeSubscriptionSource();
            _dummyCommand = A.Fake<Command<KeyValueStore<int>>>();

            var dummyModel = new KeyValueStore<int>();
            var dummyWriter = A.Fake<IJournalWriter>();
            _now = DateTime.Now;

            _engine = new Engine<KeyValueStore<int>>(_settings, dummyModel, _fakeSource, dummyWriter, 0);
        }

        private class FakeSubscriptionSource : IJournalSubscriptionSource
        {
            internal Action<JournalRecord> RecordHandler { get; private set; }

            public IJournalSubscription Subscribe(long from, Action<JournalRecord> handler)
            {
                RecordHandler = handler;
                return new JournalSubscription(_ => { }, from, _ => { });
            }
        }
    }
}
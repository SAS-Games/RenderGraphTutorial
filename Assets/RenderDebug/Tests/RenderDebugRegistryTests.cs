using NUnit.Framework;
using UnityEngine;

namespace SAS.RenderDebugging.Tests
{
    public sealed class RenderDebugRegistryTests
    {
        private sealed class Source : IRenderDebugSource
        {
            public Source(string id, string name)
            {
                DebugId = id;
                DisplayName = name;
            }

            public string DebugId { get; }
            public string DisplayName { get; }
        }

        private sealed class UnitySource : ScriptableObject, IRenderDebugSource
        {
            public string DebugId => "destroyable";
            public string DisplayName => "Destroyable";
        }

        private RenderDebugRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new RenderDebugRegistry(_ => { });
        }

        [TearDown]
        public void TearDown()
        {
            _registry.Dispose();
        }

        [Test]
        public void SourceRegistrationAndUnregistrationAreDeterministic()
        {
            Source source = new("source", "Source");

            Assert.That(_registry.RegisterSource(source), Is.True);
            Assert.That(_registry.RegisterSource(source), Is.True);
            Assert.That(_registry.RegisterSource(new Source("source", "Duplicate")), Is.False);
            Assert.That(_registry.Sources, Has.Count.EqualTo(1));
            Assert.That(_registry.UnregisterSource(source.DebugId, source), Is.True);
            Assert.That(_registry.Sources, Is.Empty);
        }

        [Test]
        public void StagesUseExplicitOrderThenStableId()
        {
            Source source = RegisterSource();
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("late", "Late", 30));
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("beta", "Beta", 10));
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("alpha", "Alpha", 10));

            Assert.That(_registry.TryGetSource(source.DebugId, out RenderDebugSourceRecord record), Is.True);
            Assert.That(record.Stages[0].Descriptor.Id, Is.EqualTo("alpha"));
            Assert.That(record.Stages[1].Descriptor.Id, Is.EqualTo("beta"));
            Assert.That(record.Stages[2].Descriptor.Id, Is.EqualTo("late"));
        }

        [Test]
        public void DuplicateStageKeepsOriginalMetadata()
        {
            Source source = RegisterSource();
            RenderDebugStage original = new("mask", "Mask", 10);
            RenderDebugStage duplicate = new("mask", "Different", 99);

            Assert.That(_registry.RegisterStage(source.DebugId, original), Is.True);
            Assert.That(_registry.RegisterStage(source.DebugId, original), Is.True);
            Assert.That(_registry.RegisterStage(source.DebugId, duplicate), Is.False);
            Assert.That(_registry.TryGetStage(source.DebugId, "mask", out RenderDebugStageRecord record), Is.True);
            Assert.That(record.Descriptor.DisplayName, Is.EqualTo("Mask"));
            Assert.That(record.Descriptor.Order, Is.EqualTo(10));
        }

        [Test]
        public void RequestedStageTrackingIsExplicit()
        {
            Source source = RegisterSource();
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("mask", "Mask", 10));

            Assert.That(_registry.IsStageRequested(source.DebugId, "mask"), Is.False);
            Assert.That(_registry.SetStageRequested(source.DebugId, "mask", true), Is.True);
            Assert.That(_registry.IsStageRequested(source.DebugId, "mask"), Is.True);
            _registry.ClearStageRequests();
            Assert.That(_registry.IsStageRequested(source.DebugId, "mask"), Is.False);
        }

        [Test]
        public void CaptureRequestsEveryStageAndFreezesAfterDataArrives()
        {
            Source source = RegisterSource();
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("a", "A", 10));
            _registry.RegisterStage(source.DebugId, new RenderDebugStage("b", "B", 20));
            Texture2D texture = new(1, 1);

            try
            {
                Assert.That(_registry.BeginCapture(source.DebugId), Is.True);
                Assert.That(_registry.ViewMode, Is.EqualTo(RenderDebugViewMode.CapturePending));
                Assert.That(_registry.IsStageRequested(source.DebugId, "a"), Is.True);
                Assert.That(_registry.IsStageRequested(source.DebugId, "b"), Is.True);

                RenderDebugTextureData data = new(
                    source.DebugId,
                    "a",
                    texture,
                    RenderDebugTextureMetadata.FromTexture(texture),
                    1,
                    0,
                    string.Empty,
                    true);
                Assert.That(_registry.PublishTextureData(data), Is.True);
                Assert.That(_registry.CompletePendingCapture(), Is.True);
                Assert.That(_registry.ViewMode, Is.EqualTo(RenderDebugViewMode.Captured));
                Assert.That(_registry.IsStageRequested(source.DebugId, "a"), Is.False);

                _registry.ReturnToLive();
                Assert.That(_registry.ViewMode, Is.EqualTo(RenderDebugViewMode.Live));
                Assert.That(_registry.TryGetStage(source.DebugId, "a", out RenderDebugStageRecord record), Is.True);
                Assert.That(record.HasTextureData, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void DestroyedUnitySourceIsPruned()
        {
            UnitySource source = ScriptableObject.CreateInstance<UnitySource>();
            Assert.That(_registry.RegisterSource(source), Is.True);

            Object.DestroyImmediate(source);

            Assert.That(_registry.PruneDestroyedSources(), Is.EqualTo(1));
            Assert.That(_registry.Sources, Is.Empty);
        }

        private Source RegisterSource()
        {
            Source source = new("source", "Source");
            Assert.That(_registry.RegisterSource(source), Is.True);
            return source;
        }
    }
}

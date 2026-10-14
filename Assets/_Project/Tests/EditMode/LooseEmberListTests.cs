using Cinderbird.Embers;
using Cinderbird.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class LooseEmberListTests
    {
        private static Ember At(float x)
        {
            return new Ember(new Vector3(x, 0f, 0f), 0, 0);
        }

        [Test]
        public void Add_GivesEachEmberItsOwnId()
        {
            LooseEmberList list = new LooseEmberList();

            int a = list.Add(At(1f), Vector3.zero);
            int b = list.Add(At(2f), Vector3.zero);

            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(list[list.IndexOf(a)].Ember.Position.x, Is.EqualTo(1f));
            Assert.That(list[list.IndexOf(b)].Ember.Position.x, Is.EqualTo(2f));
        }

        [Test]
        public void RemoveAt_KeepsTheOtherIdsFindable()
        {
            LooseEmberList list = new LooseEmberList();
            int a = list.Add(At(1f), Vector3.zero);
            int b = list.Add(At(2f), Vector3.zero);
            int c = list.Add(At(3f), Vector3.zero);

            list.RemoveAt(list.IndexOf(a));

            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list.IndexOf(a), Is.EqualTo(-1));
            Assert.That(list[list.IndexOf(b)].Ember.Position.x, Is.EqualTo(2f));
            Assert.That(list[list.IndexOf(c)].Ember.Position.x, Is.EqualTo(3f));
        }

        [Test]
        public void RemoveAt_TheLast_Works()
        {
            LooseEmberList list = new LooseEmberList();
            int a = list.Add(At(1f), Vector3.zero);
            int b = list.Add(At(2f), Vector3.zero);

            list.RemoveAt(list.IndexOf(b));

            Assert.That(list.IndexOf(b), Is.EqualTo(-1));
            Assert.That(list.IndexOf(a), Is.EqualTo(0));
        }

        [Test]
        public void MoveTo_KeepsIdAndWhereItCameFrom()
        {
            LooseEmberList list = new LooseEmberList();
            int a = list.Add(At(1f), Vector3.one);

            list.MoveTo(list.IndexOf(a), new Vector3(5f, 0f, 0f));

            LooseEmber moved = list[list.IndexOf(a)];
            Assert.That(moved.Id, Is.EqualTo(a));
            Assert.That(moved.Ember.Position.x, Is.EqualTo(5f));
            Assert.That(moved.CameFrom, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Clear_ForgetsEveryId_ButNewIdsStayUnique()
        {
            LooseEmberList list = new LooseEmberList();
            int a = list.Add(At(1f), Vector3.zero);

            list.Clear();
            int b = list.Add(At(2f), Vector3.zero);

            Assert.That(list.IndexOf(a), Is.EqualTo(-1));
            Assert.That(b, Is.Not.EqualTo(a));
        }
    }
}

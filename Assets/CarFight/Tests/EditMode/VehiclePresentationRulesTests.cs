using CarFight.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Presentation
{
    public sealed class VehiclePresentationRulesTests
    {
        [Test]
        public void FullMediumSpeedTurnReachesElevenDegreesOfBodyRoll()
        {
            float roll = VehiclePresentationRules.ChassisRollTarget(1.85f, 8f);

            Assert.That(Mathf.Abs(roll * Mathf.Rad2Deg), Is.EqualTo(11f).Within(0.001f));
            Assert.That(VehiclePresentationRules.ChassisRollTarget(1.85f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void HardHighSpeedBrakePitchesForwardButOrdinaryBrakingDoesNot()
        {
            float hardPitch = VehiclePresentationRules.ChassisBrakePitchTarget(1f, 18f);
            float ordinaryPitch = VehiclePresentationRules.ChassisBrakePitchTarget(0.70f, 18f);
            float buildingPitch = Mathf.Abs(
                VehiclePresentationRules.ChassisBrakePitchTarget(0.85f, 18f));

            Assert.That(hardPitch * Mathf.Rad2Deg, Is.EqualTo(-18f).Within(0.001f));
            Assert.That(ordinaryPitch, Is.EqualTo(0f));
            Assert.That(buildingPitch, Is.GreaterThan(0f).And.LessThan(10f * Mathf.Deg2Rad));
        }

        [Test]
        public void HardBrakeLocksOnlyVisualWheelSpin()
        {
            Assert.That(VehiclePresentationRules.WheelSpinScale(0f), Is.EqualTo(1f));
            Assert.That(VehiclePresentationRules.WheelSpinScale(1f), Is.EqualTo(0f));
            Assert.That(VehiclePresentationRules.VisualSteerTarget(1.85f) * Mathf.Rad2Deg,
                Is.EqualTo(30f).Within(0.001f));
        }
    }
}

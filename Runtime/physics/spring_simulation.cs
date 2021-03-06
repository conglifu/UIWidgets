using System;
using Unity.UIWidgets.foundation;

namespace Unity.UIWidgets.physics {
    public class SpringDescription {
        public SpringDescription(
            double mass,
            double stiffness,
            double damping
        ) {
            this.mass = mass;
            this.stiffness = stiffness;
            this.damping = damping;
        }

        public static SpringDescription withDampingRatio(
            double mass,
            double stiffness,
            double ratio = 1.0
        ) {
            var damping = ratio * 2.0 * Math.Sqrt(mass * stiffness);
            return new SpringDescription(mass, stiffness, damping);
        }

        public readonly double mass;

        public readonly double stiffness;

        public readonly double damping;

        public override string ToString() {
            return $"{this.GetType()}(mass {this.mass:F1}, stiffness: {this.stiffness:F1}, damping: {this.damping:F1})";
        }
    }

    public enum SpringType {
        criticallyDamped,
        underDamped,
        overDamped,
    }

    public class SpringSimulation : Simulation {
        public SpringSimulation(
            SpringDescription spring,
            double start,
            double end,
            double velocity,
            Tolerance tolerance = null
        ) : base(tolerance: tolerance) {
            this._endPosition = end;
            this._solution = _SpringSolution.create(spring, start - end, velocity);
        }

        protected readonly double _endPosition;
        readonly _SpringSolution _solution;

        public SpringType type {
            get { return this._solution.type; }
        }

        public override double x(double time) {
            return this._endPosition + this._solution.x(time);
        }

        public override double dx(double time) {
            return this._solution.dx(time);
        }

        public override bool isDone(double time) {
            return PhysicsUtils.nearZero(this._solution.x(time), this.tolerance.distance) &&
                   PhysicsUtils.nearZero(this._solution.dx(time), this.tolerance.velocity);
        }

        public override string ToString() {
            return $"{this.GetType()}(end: {this._endPosition}, {this.type}";
        }
    }

    public class ScrollSpringSimulation : SpringSimulation {
        public ScrollSpringSimulation(
            SpringDescription spring,
            double start,
            double end,
            double velocity,
            Tolerance tolerance = null
        ) : base(spring, start, end, velocity, tolerance: tolerance) {
        }

        public override double x(double time) {
            return this.isDone(time) ? this._endPosition : base.x(time);
        }
    }

    abstract class _SpringSolution {
        internal static _SpringSolution create(
            SpringDescription spring,
            double initialPosition,
            double initialVelocity
        ) {
            D.assert(spring != null);
            double cmk = spring.damping * spring.damping - 4 * spring.mass * spring.stiffness;

            if (cmk == 0.0) {
                return _CriticalSolution.create(spring, initialPosition, initialVelocity);
            }

            if (cmk > 0.0) {
                return _OverdampedSolution.create(spring, initialPosition, initialVelocity);
            }

            return _UnderdampedSolution.create(spring, initialPosition, initialVelocity);
        }

        public abstract double x(double time);
        public abstract double dx(double time);
        public abstract SpringType type { get; }
    }

    class _CriticalSolution : _SpringSolution {
        internal new static _CriticalSolution create(
            SpringDescription spring,
            double distance,
            double velocity
        ) {
            double r = -spring.damping / (2.0 * spring.mass);
            double c1 = distance;
            double c2 = velocity / (r * distance);
            return new _CriticalSolution(r, c1, c2);
        }

        _CriticalSolution(
            double r, double c1, double c2
        ) {
            this._r = r;
            this._c1 = c1;
            this._c2 = c2;
        }

        readonly double _r, _c1, _c2;

        public override double x(double time) {
            return (this._c1 + this._c2 * time) * Math.Pow(Math.E, this._r * time);
        }

        public override double dx(double time) {
            double power = Math.Pow(Math.E, this._r * time);
            return this._r * (this._c1 + this._c2 * time) * power + this._c2 * power;
        }

        public override SpringType type {
            get { return SpringType.criticallyDamped; }
        }
    }

    class _OverdampedSolution : _SpringSolution {
        internal new static _OverdampedSolution create(
            SpringDescription spring,
            double distance,
            double velocity
        ) {
            double cmk = spring.damping * spring.damping - 4 * spring.mass * spring.stiffness;
            double r1 = (-spring.damping - Math.Sqrt(cmk)) / (2.0 * spring.mass);
            double r2 = (-spring.damping + Math.Sqrt(cmk)) / (2.0 * spring.mass);
            double c2 = (velocity - r1 * distance) / (r2 - r1);
            double c1 = distance - c2;
            return new _OverdampedSolution(r1, r2, c1, c2);
        }

        _OverdampedSolution(
            double r1, double r2, double c1, double c2
        ) {
            this._r1 = r1;
            this._r2 = r2;
            this._c1 = c1;
            this._c2 = c2;
        }

        readonly double _r1, _r2, _c1, _c2;

        public override double x(double time) {
            return this._c1 * Math.Pow(Math.E, this._r1 * time) +
                   this._c2 * Math.Pow(Math.E, this._r2 * time);
        }

        public override double dx(double time) {
            return this._c1 * this._r1 * Math.Pow(Math.E, this._r1 * time) +
                   this._c2 * this._r2 * Math.Pow(Math.E, this._r2 * time);
        }

        public override SpringType type {
            get { return SpringType.overDamped; }
        }
    }

    class _UnderdampedSolution : _SpringSolution {
        internal new static _UnderdampedSolution create(
            SpringDescription spring,
            double distance,
            double velocity
        ) {
            double w = Math.Sqrt(4.0 * spring.mass * spring.stiffness -
                                 spring.damping * spring.damping) / (2.0 * spring.mass);
            double r = -(spring.damping / 2.0 * spring.mass);
            double c1 = distance;
            double c2 = (velocity - r * distance) / w;
            return new _UnderdampedSolution(w, r, c1, c2);
        }

        _UnderdampedSolution(
            double w, double r, double c1, double c2
        ) {
            this._w = w;
            this._r = r;
            this._c1 = c1;
            this._c2 = c2;
        }

        readonly double _w, _r, _c1, _c2;

        public override double x(double time) {
            return Math.Pow(Math.E, this._r * time) *
                   (this._c1 * Math.Cos(this._w * time) + this._c2 * Math.Sin(this._w * time));
        }

        public override double dx(double time) {
            double power = Math.Pow(Math.E, this._r * time);
            double cosine = Math.Cos(this._w * time);
            double sine = Math.Sin(this._w * time);
            return power * (this._c2 * this._w * cosine - this._c1 * this._w * sine) +
                   this._r * power * (this._c2 * sine + this._c1 * cosine);
        }

        public override SpringType type {
            get { return SpringType.underDamped; }
        }
    }
}
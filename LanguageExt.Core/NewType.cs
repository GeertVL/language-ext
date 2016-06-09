﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;
using System.Reflection;

namespace LanguageExt
{
    public interface INewType
    {
    }

    /// <summary>
    /// NewType - inspired by Haskell's 'newtype' keyword.
    /// https://wiki.haskell.org/Newtype
    /// Derive type from this one to get: Equatable, Comparable, Appendable, Subtractable, 
    /// Multiplicable, Divisible strongly typed values.  For example:
    ///     class Metres : NewType<double>
    ///     class Hours : NewType<double>
    /// Will not accept null values
    /// </summary>
    public abstract class NewType<T> : 
        IEquatable<NewType<T>>, 
        IComparable<NewType<T>>,
#if !COREFX
        IAppendable<NewType<T>>,
        ISubtractable<NewType<T>>,
        IMultiplicable<NewType<T>>,
        IDivisible<NewType<T>>,
#endif
        INewType
    {
        public readonly T Value;

        public NewType(T value)
        {
            if (isnull(value)) throw new ArgumentNullException(nameof(value));
            Value = value;
        }

        public int CompareTo(NewType<T> other) =>
            !ReferenceEquals(other, null) &&
            GetType() == other.GetType()
                ? Comparer<T>.Default.Compare(Value, other.Value)
                : failwith<int>("Mismatched NewTypes used in comparison");

        public bool Equals(NewType<T> other) =>
            !ReferenceEquals(other, null) &&
            GetType() == other.GetType() &&
            Value.Equals(other.Value);

        public override bool Equals(object obj) =>
            !ReferenceEquals(obj, null) &&
            obj is NewType<T> &&
            Equals((NewType<T>)obj);

        public override int GetHashCode() =>
            Value == null ? 0 : Value.GetHashCode();

        public static bool operator ==(NewType<T> lhs, NewType<T> rhs) =>
            lhs.Equals(rhs);

        public static bool operator !=(NewType<T> lhs, NewType<T> rhs) =>
            !lhs.Equals(rhs);

        public static bool operator >(NewType<T> lhs, NewType<T> rhs) =>
            !ReferenceEquals(lhs, null) &&
            !ReferenceEquals(rhs, null) &&
            lhs.CompareTo(rhs) > 0;

        public static bool operator >=(NewType<T> lhs, NewType<T> rhs) =>
            !ReferenceEquals(lhs, null) &&
            !ReferenceEquals(rhs, null) &&
            lhs.CompareTo(rhs) >= 0;

        public static bool operator <(NewType<T> lhs, NewType<T> rhs) =>
            !ReferenceEquals(lhs, null) &&
            !ReferenceEquals(rhs, null) &&
            lhs.CompareTo(rhs) < 0;

        public static bool operator <=(NewType<T> lhs, NewType<T> rhs) =>
            !ReferenceEquals(lhs, null) &&
            !ReferenceEquals(rhs, null) &&
            lhs.CompareTo(rhs) <= 0;

        public NewType<T> Bind(Func<T, NewType<T>> bind)
        {
            var ures = bind(Value);
            if (GetType() != ures.GetType()) throw new Exception("LINQ statement with mismatched NewTypes");
            return ures;
        }

        public bool Exists(Func<T, bool> predicate) =>
            predicate(Value);

        public bool ForAll(Func<T, bool> predicate) =>
            predicate(Value);

        public int Count() => 1;

#if !COREFX
        public NewType<T> Map(Func<T, T> map) =>
            Select(map);

        public static NewType<T> operator +(NewType<T> lhs, NewType<T> rhs) =>
            lhs.Append(rhs);

        public static NewType<T> operator -(NewType<T> lhs, NewType<T> rhs) =>
            lhs.Subtract(rhs);

        public static NewType<T> operator /(NewType<T> lhs, NewType<T> rhs) =>
            lhs.Divide(rhs);

        public static NewType<T> operator *(NewType<T> lhs, NewType<T> rhs) =>
            lhs.Multiply(rhs);

        public NewType<T> Select(Func<T, T> map) =>
            (NewType<T>)NewType.Construct(GetType(), map(Value));

        public NewType<T> SelectMany(
            Func<T, NewType<T>> bind,
            Func<T, T, T> project
            )
        {
            var ures = bind(Value);
            if (GetType() != ures.GetType()) throw new Exception("LINQ statement with mismatched NewTypes");
            return (NewType<T>)NewType.Construct(GetType(), project(Value, ures.Value));
        }

        public NewType<T> Append(NewType<T> rhs) =>
            GetType() == rhs.GetType()
                ? (NewType<T>)NewType.Construct(GetType(), TypeDesc.Append(Value, rhs.Value, TypeDesc<T>.Default))
                : failwith<NewType<T>>("Mismatched NewTypes in append/add");

        public NewType<T> Subtract(NewType<T> rhs) =>
            GetType() == rhs.GetType()
                ? (NewType<T>)NewType.Construct(GetType(), TypeDesc.Subtract(Value, rhs.Value, TypeDesc<T>.Default))
                : failwith<NewType<T>>("Mismatched NewTypes in subtract");

        public NewType<T> Divide(NewType<T> rhs) =>
            GetType() == rhs.GetType()
                ? (NewType<T>)NewType.Construct(GetType(), TypeDesc.Divide(Value, rhs.Value, TypeDesc<T>.Default))
                : failwith<NewType<T>>("Mismatched NewTypes in divide");

        public NewType<T> Multiply(NewType<T> rhs) =>
            GetType() == rhs.GetType()
                ? (NewType<T>)NewType.Construct(GetType(), TypeDesc.Multiply(Value, rhs.Value, TypeDesc<T>.Default))
                : failwith<NewType<T>>("Mismatched NewTypes in multiply");

#endif

        public Unit Iter(Action<T> f)
        {
            f(Value);
            return unit;
        }

        public NT As<NT>() where NT : NewType<T> =>
            GetType() == typeof(NT)
                ? (NT)this
                : failwith<NT>("Mismatched NewTypes cast");

        public override string ToString() =>
            $"{GetType().Name}({Value})";
    }

#if !COREFX
    internal static class NewType
    {
        static Map<Type, ConstructorInfo> constructors = Map.empty<Type, ConstructorInfo>();
        private static ConstructorInfo GetCtor(Type newType)
        {
            if (newType.Name == "NewType") throw new ArgumentException("Only use NewType.Contruct to build construct types derived from NewType<T>");
            var ctors = (from c in newType.GetTypeInfo().GetConstructors()
                         where c.GetParameters().Length == 1
                         select c)
                        .ToArray();

            if (ctors.Length == 0) throw new ArgumentException($"{newType.FullName} hasn't any one-argument constructors");
            if (ctors.Length > 1) throw new ArgumentException($"{newType.FullName} has more than one constructor with 1 parameter");

            var ctor = ctors.First();
            constructors = constructors.AddOrUpdate(newType, ctor);
            return ctor;
        }

        public static object Construct(Type newTypeT, object arg) =>
            constructors.Find(newTypeT).IfNone(() => GetCtor(newTypeT)).Invoke(new object[] { arg });

        public static NewTypeT Construct<NewTypeT, T>(T arg) where NewTypeT : NewType<T> =>
            (NewTypeT)constructors.Find(typeof(NewTypeT)).IfNone(() => GetCtor(typeof(NewTypeT))).Invoke(new object[] { arg });
    }
#endif
}

public static class __NewTypeExts
{
    public static S Fold<T, S>(this NewType<T> self, S state, Func<S, T, S> folder) =>
        folder(state, self.Value);

    public static int Sum(this NewType<int> self) =>
        self.Value;
}
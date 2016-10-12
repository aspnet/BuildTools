using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ApiCheck.Baseline
{
    public class MemberDescriptor : ApiElement
    {
        public override string Id
        {
            get
            {
                return string.Join(" ", GetComponents());
            }
        }

        private IEnumerable<string> GetComponents()
        {
            if (ExplicitInterface == null && Visibility != null)
            {
                switch (Visibility)
                {
                    case ApiElementVisibility.Public:
                        yield return "public";
                        break;
                    case ApiElementVisibility.Protected:
                        yield return "protected";
                        break;
                    case ApiElementVisibility.Internal:
                        yield return "internal";
                        break;
                    case ApiElementVisibility.ProtectedInternal:
                        yield return "protected";
                        yield return "internal";
                        break;
                    case ApiElementVisibility.Private:
                        yield return "private";
                        break;
                    default:
                        break;
                }
            }

            if (Constant)
            {
                yield return "const";
            }
            else
            {
                if (Static)
                {
                    yield return "static";
                }

                if (ReadOnly)
                {
                    yield return "readonly";
                }
            }

            if (Abstract)
            {
                yield return "abstract";
            }

            if (Sealed && ImplementedInterface == null)
            {
                yield return "sealed";
            }

            if (!Sealed && Virtual && !Abstract && !Override && ImplementedInterface == null)
            {
                yield return "virtual";
            }

            if (Override)
            {
                yield return "override";
            }

            if (New)
            {
                yield return "new";
            }

            if (ReturnType != null)
            {
                yield return ReturnType;
            }

            if (Kind != MemberKind.Field)
            {
                var name = ExplicitInterface != null ? $"{ExplicitInterface}.{Name}" : Name;
                yield return GetParametersComponent(Name);

                foreach (var constraint in GenericConstraints)
                {
                    yield return "where";
                    yield return constraint.Id;
                }
            }
            else
            {
                yield return Name;

                if (Literal != null)
                {
                    yield return "=";
                    yield return Literal;
                }
            }
        }

        private string GetParametersComponent(string name)
        {
            var builder = new StringBuilder();

            builder.Append(name);
            builder.Append("(");
            for (int i = 0; i < Parameters.Count; i++)
            {
                var parameter = Parameters[i];
                if (Extension && i == 0)
                {
                    builder.Append("this ");
                }
                builder.Append(parameter.Id);
                if (i < Parameters.Count - 1)
                {
                    builder.Append(", ");
                }
            }

            builder.Append(")");
            return builder.ToString();
        }

        public MemberKind Kind { get; set; }
        public string Name { get; set; }
        public IList<ParameterDescriptor> Parameters { get; set; } = new List<ParameterDescriptor>();
        public string ReturnType { get; set; }
        public bool Sealed { get; set; }
        public bool Static { get; set; }
        public bool Virtual { get; set; }
        public bool Override { get; set; }
        public bool Abstract { get; set; }
        public bool New { get; set; }
        public bool Extension { get; set; }
        public bool ReadOnly { get; set; }
        public string ExplicitInterface { get; set; }
        public string ImplementedInterface { get; set; }
        public ApiElementVisibility? Visibility { get; set; }
        public IList<GenericConstraintDescriptor> GenericConstraints { get; } = new List<GenericConstraintDescriptor>();
        public bool Constant { get; set; }
        public string Literal { get; set; }

        public static string GetMemberNameFor(MethodBase member)
        {
            if (!member.IsGenericMethod)
            {
                return member.Name;
            }

            var genericParameters = string.Join(", ", member.GetGenericArguments().Select(ga => TypeDescriptor.GetTypeNameFor(ga.GetTypeInfo())));

            return $"{member.Name}<{genericParameters}>";
        }
    }
}

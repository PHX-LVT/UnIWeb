using System.Reflection;

namespace FullProject.Utils
{
    public static class SchemaNormalizer
    {
        public static T Normalize<T>(T obj) where T : class, new()
        {
            if (obj == null)
                obj = new T();

            NormalizeObject(obj);
            return obj;
        }

        private static void NormalizeObject(object obj)
        {
            var properties = obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var type = prop.PropertyType;
                var value = prop.GetValue(obj);

                // Skip string (important)
                if (type == typeof(string))
                    continue;

                // If null → initialize object (ONLY if allowed)
                if (value == null)
                {
                    if (ShouldInstantiate(prop))
                    {
                        var instance = Activator.CreateInstance(type);
                        prop.SetValue(obj, instance);
                        value = instance;
                    }
                }

                // Recursively normalize nested objects
                if (value != null && type.IsClass && type != typeof(string))
                {
                    NormalizeObject(value);
                }
            }
        }

        private static bool ShouldInstantiate(PropertyInfo prop)
        {
            // If marked to keep null → DO NOT create object
            var keepNull = prop.GetCustomAttribute<KeepNullAttribute>() != null;
            if (keepNull) return false;

            var type = prop.PropertyType;

            return type.IsClass &&
                   type != typeof(string) &&
                   type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
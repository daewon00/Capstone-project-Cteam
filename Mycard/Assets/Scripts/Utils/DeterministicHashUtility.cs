using System;

namespace Game.Utils
{
    /// <summary>
    /// Provides deterministic hash utilities that remain stable across platforms and runtime sessions.
    /// </summary>
    public static class DeterministicHashUtility
    {
        /// <summary>
        /// Computes a deterministic 32-bit hash for the provided key and optional salt.
        /// The result is suitable for use with System.Random (non-negative and non-zero).
        /// </summary>
        public static int HashToSeed(string key, string salt = "")
        {
            unchecked
            {
                const uint basis = 2166136261u;
                const uint prime = 16777619u;

                uint hash = basis;

                if (!string.IsNullOrEmpty(key))
                {
                    foreach (char c in key)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                }

                if (!string.IsNullOrEmpty(salt))
                {
                    foreach (char c in salt)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                }

                // System.Random requires a non-negative, non-zero seed.
                int result = (int)(hash & 0x7FFFFFFF);
                if (result == 0)
                {
                    result = 1;
                }
                return result;
            }
        }

        /// <summary>
        /// Computes a deterministic unsigned hash, useful when an unsigned seed is required.
        /// </summary>
        public static uint HashToUInt(string key, string salt = "")
        {
            unchecked
            {
                const uint basis = 2166136261u;
                const uint prime = 16777619u;

                uint hash = basis;

                if (!string.IsNullOrEmpty(key))
                {
                    foreach (char c in key)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                }

                if (!string.IsNullOrEmpty(salt))
                {
                    foreach (char c in salt)
                    {
                        hash ^= c;
                        hash *= prime;
                    }
                }

                return hash == 0u ? 1u : hash;
            }
        }
    }
}

// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using BenchmarkDotNet.Attributes;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class HighestOneBitBenchmark
    {
        // JBIG2 filter
        private const int end = 65_536;

        [Benchmark(Baseline = true)]
        public long HighestOneBit()
        {
            long sum = 0;
            for (int i = -end; i <= end; ++i)
            {
                sum += HighestOneBitLocal(i);
            }
            return sum;
        }

        [Benchmark]
        public long HighestOneBitNoString()
        {
            long sum = 0;
            for (int i = -end; i <= end; ++i)
            {
                sum += HighestOneBit(i);
            }
            return sum;
        }

        public static int HighestOneBit(int number)
        {
            if (number == 0) return 1;
            return (int)Math.Pow(2, Math.Floor(Math.Log2(number)));
        }

        public static int HighestOneBitLocal(int number)
        {
            return (int)Math.Pow(2, Convert.ToString(number, 2).Length - 1);
        }
    }
}

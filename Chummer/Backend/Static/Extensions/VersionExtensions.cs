/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Runtime.CompilerServices;

namespace Chummer
{
    public static class VersionExtensions
    {
        /// <summary>
        /// Expanded version of Version.TryParse that can handle numbers with no decimals
        /// </summary>
        /// <param name="input">String to attempt to parse.</param>
        /// <param name="result">Version to construct. If parsing is unsuccessful, this will be null.</param>
        /// <returns>True if conversion was successful, False otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(string input, out Version result)
        {
            if (int.TryParse(input, out int intResult))
            {
                result = new Version(intResult, 0);
                return true;
            }

            return Version.TryParse(input, out result);
        }

        /// <summary>
        /// Syntactic sugar to return an allocated version as its value type equivalent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueVersion AsValue(this Version objVersion)
        {
            return new ValueVersion(objVersion);
        }
    }
}

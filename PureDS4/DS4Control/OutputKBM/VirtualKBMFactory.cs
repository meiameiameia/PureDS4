/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace DS4Windows.DS4Control
{
    /// <summary>
    /// Chooses how mapped keyboard and mouse output reaches Windows.
    ///
    /// PureDS4 sends that output through SendInput, which needs no driver and
    /// no install step. The upstream alternative, a FakerInput virtual device,
    /// was removed: it required a separate kernel driver whose wrapper
    /// binaries carried an unresolved license, and it bought only the narrow
    /// case of reaching windows that refuse simulated input.
    /// </summary>
    public static class VirtualKBMFactory
    {
        public const string DEFAULT_IDENTIFIER = "default";

        public static VirtualKBMBase DetermineHandler(string identifier =
            SendInputHandler.IDENTIFIER)
        {
            return GetFallbackHandler();
        }

        public static VirtualKBMMapping GetMappingInstance(string identifier =
            SendInputHandler.IDENTIFIER)
        {
            return GetFallbackMapping();
        }

        public static VirtualKBMBase GetFallbackHandler()
        {
            return new SendInputHandler();
        }

        public static VirtualKBMMapping GetFallbackMapping()
        {
            return new SendInputMapping();
        }

        /// <summary>
        /// Retrieves identifier string of fallback virtualkbm handler without
        /// creating an instance of the handler
        /// </summary>
        /// <returns>Identifier string of the default virtualkbm handler</returns>
        public static string GetFallbackHandlerIdentifier()
        {
            return SendInputHandler.IDENTIFIER;
        }

        public static bool IsValidHandler(string identifier)
        {
            return identifier == SendInputHandler.IDENTIFIER;
        }
    }
}

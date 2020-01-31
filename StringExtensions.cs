﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSyntaxVisualizerUwp
{
    public static class StringExtensions
    {
        /// <summary>
        /// Given the provided string, line, and column, returns the absoluted index for the provided location. Returns -1 if out of bounds.
        /// </summary>
        /// <param name="">Text context</param>
        /// <param name="line">Line Number (1-index)</param>
        /// <param name="column">Column position on given line (1-index)</param>
        /// <returns>Overall index</returns>
        public static int GetCharacterIndex(this string str, int line, int column)
        {
            if (line == 1)
            {
                return column - 1;
            }

            var x = 0;
            var count = 2;

            while ((x = str.IndexOf("\n", x + 1)) != -1)
            {
                if (count == line)
                {
                    return x + column - 1;
                }

                count++;
            }

            return -1;
        }

        /// <summary>
        /// Given an overall index for the string, return the corresponding line/column it represents based on newlines.
        /// </summary>
        /// <param name="str">Text context.</param>
        /// <param name="index">Character index</param>
        /// <returns>Line, Column Tuple</returns>
        public static (int, int) GetLineColumnIndex(this string str, int index)
        {
            var line = 1;
            var column = 1;

            for (var i = 0; i < index; i++)
            {
                if (str[i] == '\n')
                {
                    line++;
                    column = 0;
                }

                column++;
            }

            return (line, column);
        }
    }
}

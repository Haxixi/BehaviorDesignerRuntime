using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BehaviorDesigner.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// This class encodes and decodes JSON strings.
    /// Spec. details, see http://www.json.org/
    ///
    /// JSON uses Arrays and Objects. These correspond here to the datatypes IList and IDictionary.
    /// All numbers are parsed to doubles.
    /// </summary>
    public static class MiniJSON
    {
        /// <summary>
        /// Parses the string json into a value
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <returns>An List&lt;object&gt;, a Dictionary&lt;string, object&gt;, a double, an integer,a string, null, true, or false</returns>
        public static object Deserialize(string json)
        {
            // save the string for debug information
            if (json == null)
            {
                return null;
            }

            return Parser.Parse(json);
        }

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            public static bool IsWordBreak(char c)
            {
                return Char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
            }

            enum TOKEN
            {
                NONE,
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            };

            StringReader json;

            Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                {
                    return instance.ParseValue();
                }
            }

            public void Dispose()
            {
                json.Dispose();
                json = null;
            }

            Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> table = new Dictionary<string, object>();

                // ditch opening brace
                json.Read();

                // {
                while (true)
                {
                    switch (NextToken)
                    {
                        case TOKEN.NONE:
                            return null;
                        case TOKEN.COMMA:
                            continue;
                        case TOKEN.CURLY_CLOSE:
                            return table;
                        default:
                            // name
                            string name = ParseString();
                            if (name == null)
                            {
                                return null;
                            }

                            // :
                            if (NextToken != TOKEN.COLON)
                            {
                                return null;
                            }
                            // ditch the colon
                            json.Read();

                            // value
                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                List<object> array = new List<object>();

                // ditch opening bracket
                json.Read();

                // [
                var parsing = true;
                while (parsing)
                {
                    TOKEN nextToken = NextToken;

                    switch (nextToken)
                    {
                        case TOKEN.NONE:
                            return null;
                        case TOKEN.COMMA:
                            continue;
                        case TOKEN.SQUARED_CLOSE:
                            parsing = false;
                            break;
                        default:
                            object value = ParseByToken(nextToken);

                            array.Add(value);
                            break;
                    }
                }

                return array;
            }

            object ParseValue()
            {
                TOKEN nextToken = NextToken;
                return ParseByToken(nextToken);
            }

            object ParseByToken(TOKEN token)
            {
                switch (token)
                {
                    case TOKEN.STRING:
                        return ParseString();
                    case TOKEN.NUMBER:
                        return ParseNumber();
                    case TOKEN.CURLY_OPEN:
                        return ParseObject();
                    case TOKEN.SQUARED_OPEN:
                        return ParseArray();
                    case TOKEN.TRUE:
                        return true;
                    case TOKEN.FALSE:
                        return false;
                    case TOKEN.NULL:
                        return null;
                    default:
                        return null;
                }
            }

            string ParseString()
            {
                StringBuilder s = new StringBuilder();
                char c;

                // ditch opening quote
                json.Read();

                bool parsing = true;
                while (parsing)
                {

                    if (json.Peek() == -1)
                    {
                        parsing = false;
                        break;
                    }

                    c = NextChar;
                    switch (c)
                    {
                        case '"':
                            parsing = false;
                            break;
                        case '\\':
                            if (json.Peek() == -1)
                            {
                                parsing = false;
                                break;
                            }

                            c = NextChar;
                            switch (c)
                            {
                                case '"':
                                case '\\':
                                case '/':
                                    s.Append(c);
                                    break;
                                case 'b':
                                    s.Append('\b');
                                    break;
                                case 'f':
                                    s.Append('\f');
                                    break;
                                case 'n':
                                    s.Append('\n');
                                    break;
                                case 'r':
                                    s.Append('\r');
                                    break;
                                case 't':
                                    s.Append('\t');
                                    break;
                                case 'u':
                                    var hex = new char[4];

                                    for (int i = 0; i < 4; i++)
                                    {
                                        hex[i] = NextChar;
                                    }

                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default:
                            s.Append(c);
                            break;
                    }
                }

                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;

                if (number.IndexOf('.') == -1)
                {
                    long parsedInt;
                    Int64.TryParse(number, out parsedInt);
                    return parsedInt;
                }

                double parsedDouble;
                Double.TryParse(number, out parsedDouble);
                return parsedDouble;
            }

            void EatWhitespace()
            {
                while (Char.IsWhiteSpace(PeekChar))
                {
                    json.Read();

                    if (json.Peek() == -1)
                    {
                        break;
                    }
                }
            }

            char PeekChar
            {
                get
                {
                    return Convert.ToChar(json.Peek());
                }
            }

            char NextChar
            {
                get
                {
                    return Convert.ToChar(json.Read());
                }
            }

            string NextWord
            {
                get
                {
                    StringBuilder word = new StringBuilder();

                    while (!IsWordBreak(PeekChar))
                    {
                        word.Append(NextChar);

                        if (json.Peek() == -1)
                        {
                            break;
                        }
                    }

                    return word.ToString();
                }
            }

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();

                    if (json.Peek() == -1)
                    {
                        return TOKEN.NONE;
                    }

                    switch (PeekChar)
                    {
                        case '{':
                            return TOKEN.CURLY_OPEN;
                        case '}':
                            json.Read();
                            return TOKEN.CURLY_CLOSE;
                        case '[':
                            return TOKEN.SQUARED_OPEN;
                        case ']':
                            json.Read();
                            return TOKEN.SQUARED_CLOSE;
                        case ',':
                            json.Read();
                            return TOKEN.COMMA;
                        case '"':
                            return TOKEN.STRING;
                        case ':':
                            return TOKEN.COLON;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '-':
                            return TOKEN.NUMBER;
                    }

                    switch (NextWord)
                    {
                        case "false":
                            return TOKEN.FALSE;
                        case "true":
                            return TOKEN.TRUE;
                        case "null":
                            return TOKEN.NULL;
                    }

                    return TOKEN.NONE;
                }
            }
        }

        /// <summary>
        /// Converts a IDictionary / IList object or a simple type (string, int, etc.) into a JSON string
        /// </summary>
        /// <param name="json">A Dictionary&lt;string, object&gt; / List&lt;object&gt;</param>
        /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        sealed class Serializer
        {
            StringBuilder builder;

            Serializer()
            {
                builder = new StringBuilder();
            }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();

                instance.SerializeValue(obj);

                return instance.builder.ToString();
            }

            void SerializeValue(object value)
            {
                IList asList;
                IDictionary asDict;
                string asStr;

                if (value == null)
                {
                    builder.Append("null");
                }
                else if ((asStr = value as string) != null)
                {
                    SerializeString(asStr);
                }
                else if (value is bool)
                {
                    builder.Append((bool)value ? "true" : "false");
                }
                else if ((asList = value as IList) != null)
                {
                    SerializeArray(asList);
                }
                else if ((asDict = value as IDictionary) != null)
                {
                    SerializeObject(asDict);
                }
                else if (value is char)
                {
                    SerializeString(new string((char)value, 1));
                }
                else
                {
                    SerializeOther(value);
                }
            }

            void SerializeObject(IDictionary obj)
            {
                bool first = true;

                builder.Append('{');

                foreach (object e in obj.Keys)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    SerializeString(e.ToString());
                    builder.Append(':');

                    SerializeValue(obj[e]);

                    first = false;
                }

                builder.Append('}');
            }

            void SerializeArray(IList anArray)
            {
                builder.Append('[');

                bool first = true;

                foreach (object obj in anArray)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    SerializeValue(obj);

                    first = false;
                }

                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('\"');

                char[] charArray = str.ToCharArray();
                foreach (var c in charArray)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if ((codepoint >= 32) && (codepoint <= 126))
                            {
                                builder.Append(c);
                            }
                            else
                            {
                                builder.Append("\\u");
                                builder.Append(codepoint.ToString("x4"));
                            }
                            break;
                    }
                }

                builder.Append('\"');
            }

            void SerializeOther(object value)
            {
                // NOTE: decimals lose precision during serialization.
                // They always have, I'm just letting you know.
                // Previously floats and doubles lost precision too.
                if (value is float)
                {
                    builder.Append(((float)value).ToString("R"));
                }
                else if (value is int
                  || value is uint
                  || value is long
                  || value is sbyte
                  || value is byte
                  || value is short
                  || value is ushort
                  || value is ulong)
                {
                    builder.Append(value);
                }
                else if (value is double
                  || value is decimal)
                {
                    builder.Append(Convert.ToDouble(value).ToString("R"));
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }
        }
    }
    /*public static class MiniJSON
    {
        private sealed class Parser : IDisposable
        {
            private enum TOKEN
            {
                NONE,
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            }

            private const string WORD_BREAK = "{}[],:\"";

            private StringReader json;

            private char PeekChar
            {
                get
                {
                    return Convert.ToChar(this.json.Peek());
                }
            }

            private char NextChar
            {
                get
                {
                    return Convert.ToChar(this.json.Read());
                }
            }

            private string NextWord
            {
                get
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    while (!MiniJSON.Parser.IsWordBreak(this.PeekChar))
                    {
                        stringBuilder.Append(this.NextChar);
                        if (this.json.Peek() == -1)
                        {
                            break;
                        }
                    }
                    return stringBuilder.ToString();
                }
            }

            private MiniJSON.Parser.TOKEN NextToken
            {
                get
                {
                    this.EatWhitespace();
                    if (this.json.Peek() == -1)
                    {
                        return MiniJSON.Parser.TOKEN.NONE;
                    }
                    char peekChar = this.PeekChar;
                    switch (peekChar)
                    {
                        case '"':
                            return MiniJSON.Parser.TOKEN.STRING;
                        case '#':
                        case '$':
                        case '%':
                        case '&':
                        case '\'':
                        case '(':
                        case ')':
                        case '*':
                        case '+':
                        case '.':
                        case '/':
                            IL_8D:
                            switch (peekChar)
                            {
                                case '[':
                                    return MiniJSON.Parser.TOKEN.SQUARED_OPEN;
                                case '\\':
                                    {
                                        IL_A2:
                                        switch (peekChar)
                                        {
                                            case '{':
                                                return MiniJSON.Parser.TOKEN.CURLY_OPEN;
                                            case '}':
                                                this.json.Read();
                                                return MiniJSON.Parser.TOKEN.CURLY_CLOSE;
                                        }
                                        string nextWord = this.NextWord;
                                        switch (nextWord)
                                        {
                                            case "false":
                                                return MiniJSON.Parser.TOKEN.FALSE;
                                            case "true":
                                                return MiniJSON.Parser.TOKEN.TRUE;
                                            case "null":
                                                return MiniJSON.Parser.TOKEN.NULL;
                                        }
                                        return MiniJSON.Parser.TOKEN.NONE;
                                    }
                                case ']':
                                    this.json.Read();
                                    return MiniJSON.Parser.TOKEN.SQUARED_CLOSE;
                            }
                            goto IL_A2;
                        case ',':
                            this.json.Read();
                            return MiniJSON.Parser.TOKEN.COMMA;
                        case '-':
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            return MiniJSON.Parser.TOKEN.NUMBER;
                        case ':':
                            return MiniJSON.Parser.TOKEN.COLON;
                    }
                    goto IL_8D;
                }
            }

            private Parser(string jsonString)
            {
                this.json = new StringReader(jsonString);
            }

            public static bool IsWordBreak(char c)
            {
                return char.IsWhiteSpace(c) || "{}[],:\"".IndexOf(c) != -1;
            }

            public static object Parse(string jsonString)
            {
                object result;
                using (MiniJSON.Parser parser = new MiniJSON.Parser(jsonString))
                {
                    result = parser.ParseValue();
                }
                return result;
            }

            public void Dispose()
            {
                this.json.Dispose();
                this.json = null;
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> dictionary = new Dictionary<string, object>();
                this.json.Read();
                while (true)
                {
                    MiniJSON.Parser.TOKEN nextToken = this.NextToken;
                    switch (nextToken)
                    {
                        case MiniJSON.Parser.TOKEN.NONE:
                            goto IL_37;
                        case MiniJSON.Parser.TOKEN.CURLY_OPEN:
                            {
                                IL_2B:
                                if (nextToken == MiniJSON.Parser.TOKEN.COMMA)
                                {
                                    continue;
                                }
                                string text = this.ParseString();
                                if (text == null)
                                {
                                    goto Block_2;
                                }
                                if (this.NextToken != MiniJSON.Parser.TOKEN.COLON)
                                {
                                    goto Block_3;
                                }
                                this.json.Read();
                                dictionary[text] = this.ParseValue();
                                continue;
                            }
                        case MiniJSON.Parser.TOKEN.CURLY_CLOSE:
                            return dictionary;
                    }
                    goto IL_2B;
                }
                IL_37:
                return null;
                Block_2:
                return null;
                Block_3:
                return null;
            }

            private List<object> ParseArray()
            {
                List<object> list = new List<object>();
                this.json.Read();
                bool flag = true;
                while (flag)
                {
                    MiniJSON.Parser.TOKEN nextToken = this.NextToken;
                    MiniJSON.Parser.TOKEN tOKEN = nextToken;
                    switch (tOKEN)
                    {
                        case MiniJSON.Parser.TOKEN.SQUARED_CLOSE:
                            flag = false;
                            continue;
                        case MiniJSON.Parser.TOKEN.COLON:
                            IL_38:
                            if (tOKEN != MiniJSON.Parser.TOKEN.NONE)
                            {
                                object item = this.ParseByToken(nextToken);
                                list.Add(item);
                                continue;
                            }
                            return null;
                        case MiniJSON.Parser.TOKEN.COMMA:
                            continue;
                    }
                    goto IL_38;
                }
                return list;
            }

            private object ParseValue()
            {
                MiniJSON.Parser.TOKEN nextToken = this.NextToken;
                return this.ParseByToken(nextToken);
            }

            private object ParseByToken(MiniJSON.Parser.TOKEN token)
            {
                switch (token)
                {
                    case MiniJSON.Parser.TOKEN.CURLY_OPEN:
                        return this.ParseObject();
                    case MiniJSON.Parser.TOKEN.SQUARED_OPEN:
                        return this.ParseArray();
                    case MiniJSON.Parser.TOKEN.STRING:
                        return this.ParseString();
                    case MiniJSON.Parser.TOKEN.NUMBER:
                        return this.ParseNumber();
                    case MiniJSON.Parser.TOKEN.TRUE:
                        return true;
                    case MiniJSON.Parser.TOKEN.FALSE:
                        return false;
                    case MiniJSON.Parser.TOKEN.NULL:
                        return null;
                }
                return null;
            }

            private string ParseString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                this.json.Read();
                bool flag = true;
                while (flag)
                {
                    if (this.json.Peek() == -1)
                    {
                        break;
                    }
                    char nextChar = this.NextChar;
                    char c = nextChar;
                    if (c != '"')
                    {
                        if (c != '\\')
                        {
                            stringBuilder.Append(nextChar);
                        }
                        else
                        {
                            if (this.json.Peek() != -1)
                            {
                                nextChar = this.NextChar;
                                char c2 = nextChar;
                                switch (c2)
                                {
                                    case 'n':
                                        stringBuilder.Append('\n');
                                        continue;
                                    case 'o':
                                    case 'p':
                                    case 'q':
                                    case 's':
                                        IL_A5:
                                        if (c2 == '"' || c2 == '/' || c2 == '\\')
                                        {
                                            stringBuilder.Append(nextChar);
                                            continue;
                                        }
                                        if (c2 == 'b')
                                        {
                                            stringBuilder.Append('\b');
                                            continue;
                                        }
                                        if (c2 != 'f')
                                        {
                                            continue;
                                        }
                                        stringBuilder.Append('\f');
                                        continue;
                                    case 'r':
                                        stringBuilder.Append('\r');
                                        continue;
                                    case 't':
                                        stringBuilder.Append('\t');
                                        continue;
                                    case 'u':
                                        {
                                            char[] array = new char[4];
                                            for (int i = 0; i < 4; i++)
                                            {
                                                array[i] = this.NextChar;
                                            }
                                            stringBuilder.Append((char)Convert.ToInt32(new string(array), 16));
                                            continue;
                                        }
                                }
                                goto IL_A5;
                            }
                            flag = false;
                        }
                    }
                    else
                    {
                        flag = false;
                    }
                }
                return stringBuilder.ToString();
            }

            private object ParseNumber()
            {
                string nextWord = this.NextWord;
                if (nextWord.IndexOf('.') == -1)
                {
                    long num;
                    long.TryParse(nextWord, NumberStyles.Any, CultureInfo.InvariantCulture, out num);
                    return num;
                }
                double num2;
                double.TryParse(nextWord, NumberStyles.Any, CultureInfo.InvariantCulture, out num2);
                return num2;
            }

            private void EatWhitespace()
            {
                while (char.IsWhiteSpace(this.PeekChar))
                {
                    this.json.Read();
                    if (this.json.Peek() == -1)
                    {
                        break;
                    }
                }
            }
        }

        private sealed class Serializer
        {
            private StringBuilder builder;

            private Serializer()
            {
                this.builder = new StringBuilder();
            }

            public static string Serialize(object obj)
            {
                MiniJSON.Serializer serializer = new MiniJSON.Serializer();
                serializer.SerializeValue(obj, 1);
                return serializer.builder.ToString();
            }

            private void SerializeValue(object value, int indentationLevel)
            {
                string str;
                IList anArray;
                IDictionary obj;
                if (value == null)
                {
                    this.builder.Append("null");
                }
                else if ((str = (value as string)) != null)
                {
                    this.SerializeString(str);
                }
                else if (value is bool)
                {
                    this.builder.Append((!(bool)value) ? "false" : "true");
                }
                else if ((anArray = (value as IList)) != null)
                {
                    this.SerializeArray(anArray, indentationLevel);
                }
                else if ((obj = (value as IDictionary)) != null)
                {
                    this.SerializeObject(obj, indentationLevel);
                }
                else if (value is char)
                {
                    this.SerializeString(new string((char)value, 1));
                }
                else
                {
                    this.SerializeOther(value);
                }
            }

            private void SerializeObject(IDictionary obj, int indentationLevel)
            {
                bool flag = true;
                this.builder.Append('{');
                this.builder.Append('\n');
                for (int i = 0; i < indentationLevel; i++)
                {
                    this.builder.Append('\t');
                }
                foreach (object current in obj.Keys)
                {
                    if (!flag)
                    {
                        this.builder.Append(',');
                        this.builder.Append('\n');
                        for (int j = 0; j < indentationLevel; j++)
                        {
                            this.builder.Append('\t');
                        }
                    }
                    this.SerializeString(current.ToString());
                    this.builder.Append(':');
                    indentationLevel++;
                    this.SerializeValue(obj[current], indentationLevel);
                    indentationLevel--;
                    flag = false;
                }
                this.builder.Append('\n');
                for (int k = 0; k < indentationLevel - 1; k++)
                {
                    this.builder.Append('\t');
                }
                this.builder.Append('}');
            }

            private void SerializeArray(IList anArray, int indentationLevel)
            {
                this.builder.Append('[');
                bool flag = true;
                for (int i = 0; i < anArray.Count; i++)
                {
                    object value = anArray[i];
                    if (!flag)
                    {
                        this.builder.Append(',');
                    }
                    this.SerializeValue(value, indentationLevel);
                    flag = false;
                }
                this.builder.Append(']');
            }

            private void SerializeString(string str)
            {
                this.builder.Append('"');
                char[] array = str.ToCharArray();
                for (int i = 0; i < array.Length; i++)
                {
                    char c = array[i];
                    char c2 = c;
                    switch (c2)
                    {
                        case '\b':
                            this.builder.Append("\\b");
                            goto IL_14C;
                        case '\t':
                            this.builder.Append("\\t");
                            goto IL_14C;
                        case '\n':
                            this.builder.Append("\\n");
                            goto IL_14C;
                        case '\v':
                            IL_44:
                            if (c2 == '"')
                            {
                                this.builder.Append("\\\"");
                                goto IL_14C;
                            }
                            if (c2 != '\\')
                            {
                                int num = Convert.ToInt32(c);
                                if (num >= 32 && num <= 126)
                                {
                                    this.builder.Append(c);
                                }
                                else
                                {
                                    this.builder.Append("\\u");
                                    this.builder.Append(num.ToString("x4"));
                                }
                                goto IL_14C;
                            }
                            this.builder.Append("\\\\");
                            goto IL_14C;
                        case '\f':
                            this.builder.Append("\\f");
                            goto IL_14C;
                        case '\r':
                            this.builder.Append("\\r");
                            goto IL_14C;
                    }
                    goto IL_44;
                    IL_14C:;
                }
                this.builder.Append('"');
            }

            private void SerializeOther(object value)
            {
                if (value is float)
                {
                    this.builder.Append(((float)value).ToString("R", CultureInfo.InvariantCulture));
                }
                else if (value is int || value is uint || value is long || value is sbyte || value is byte || value is short || value is ushort || value is ulong)
                {
                    this.builder.Append(value);
                }
                else if (value is double || value is decimal)
                {
                    this.builder.Append(Convert.ToDouble(value).ToString("R", CultureInfo.InvariantCulture));
                }
                else if (value is Vector2)
                {
                    Vector2 vector = (Vector2)value;
                    this.builder.Append(string.Concat(new string[]
                    {
                        "\"(",
                        vector.x.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector.y.ToString("R", CultureInfo.InvariantCulture),
                        ")\""
                    }));
                }
                else if (value is Vector3)
                {
                    Vector3 vector2 = (Vector3)value;
                    this.builder.Append(string.Concat(new string[]
                    {
                        "\"(",
                        vector2.x.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector2.y.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector2.z.ToString("R", CultureInfo.InvariantCulture),
                        ")\""
                    }));
                }
                else if (value is Vector4)
                {
                    Vector4 vector3 = (Vector4)value;
                    this.builder.Append(string.Concat(new string[]
                    {
                        "\"(",
                        vector3.x.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector3.y.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector3.z.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        vector3.w.ToString("R", CultureInfo.InvariantCulture),
                        ")\""
                    }));
                }
                else if (value is Quaternion)
                {
                    Quaternion quaternion = (Quaternion)value;
                    this.builder.Append(string.Concat(new string[]
                    {
                        "\"(",
                        quaternion.x.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        quaternion.y.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        quaternion.z.ToString("R", CultureInfo.InvariantCulture),
                        ",",
                        quaternion.w.ToString("R", CultureInfo.InvariantCulture),
                        ")\""
                    }));
                }
                else
                {
                    this.SerializeString(value.ToString());
                }
            }
        }

        public static object Deserialize(string json)
        {
            if (json == null)
            {
                return null;
            }
            return MiniJSON.Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return MiniJSON.Serializer.Serialize(obj);
        }
    }*/
}

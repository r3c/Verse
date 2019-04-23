using System.IO;
using System.Text;

namespace Verse.Schemas.JSON
{
	internal class ReaderState
	{
		public int Current;

		private readonly ErrorEvent error;

		private int position;

		private readonly StreamReader reader;

	    public ReaderState(Stream stream, Encoding encoding, ErrorEvent error)
		{
			this.error = error;
			this.position = 0;
			this.reader = new StreamReader(stream, encoding);

			this.Read();
		}

		public void Error(string message)
		{
			this.error(this.position, message);
		}

		public bool PullCharacter(out char character)
		{
		    var previous = this.Current;

			this.Read();

			if (previous < 0)
			{
				character = default;

				return false;
			}

			if (previous != '\\')
			{
				character = (char)previous;

				return true;
			}

			previous = this.Current;

			this.Read();

			switch (previous)
			{
				case -1:
					character = default;

					return false;

				case '"':
					character = '"';

					return true;

				case '\\':
					character = '\\';

					return true;

				case 'b':
					character = '\b';

					return true;

				case 'f':
					character = '\f';

					return true;

				case 'n':
					character = '\n';

					return true;

				case 'r':
					character = '\r';

					return true;

				case 't':
					character = '\t';

					return true;

				case 'u':
					var value = 0;

					for (var i = 0; i < 4; ++i)
					{
						previous = this.Current;

						this.Read();

					    int nibble;

					    if (previous >= '0' && previous <= '9')
							nibble = previous - '0';
						else if (previous >= 'A' && previous <= 'F')
							nibble = previous - 'A' + 10;
						else if (previous >= 'a' && previous <= 'f')
							nibble = previous - 'a' + 10;
						else
						{
							this.Error("unknown character in unicode escape sequence");

							character = default;

							return false;
						}

						value = (value << 4) + nibble;
					}

					character = (char)value;

					return true;

				default:
					character = (char)previous;

					return true;
			}
		}

		public bool PullExpected(char expected)
		{
			if (this.Current != expected)
			{
				this.Error("expected '" + expected + "'");

				return false;
			}

			this.Read();

			return true;
		}

		public void PullIgnored()
		{
		    while (this.Current >= 0 && this.Current <= ' ')
			    this.Read();
		}

		public void Read()
		{
			this.Current = this.reader.Read();

			++this.position;
		}
	}
}
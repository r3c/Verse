﻿using System;
using System.Globalization;
using System.IO;
using System.Text;
using Verse.DecoderDescriptors.Tree;
using Verse.Lookups;

namespace Verse.Schemas.JSON
{
	internal class Reader : IReader<ReaderState, JSONValue>
	{
		private readonly bool readObjectValuesAsArray;
		private readonly bool readScalarAsOneElementArray;
		private readonly Encoding encoding;

		public Reader(Encoding encoding, bool readObjectValuesAsArray, bool readScalarAsOneElementArray)
		{
			this.readObjectValuesAsArray = readObjectValuesAsArray;
			this.readScalarAsOneElementArray = readScalarAsOneElementArray;
			this.encoding = encoding;
		}

		public BrowserMove<TElement> ReadToArray<TElement>(ReaderState state, Func<TElement> constructor,
			ReaderCallback<ReaderState, JSONValue, TElement> callback)
		{
			state.PullIgnored();

			switch (state.Current)
			{
				case '[':
					return this.ReadToArrayFromArray(state, constructor, callback);

				case '{':
					if (this.readObjectValuesAsArray)
						return this.ReadToArrayFromObjectValues(state, constructor, callback);

					goto default;

				default:
					// Accept any scalar value as an array of one element
					if (this.readScalarAsOneElementArray)
					{
						return (int index, out TElement current) =>
						{
							if (index > 0)
							{
								current = default;

								return BrowserState.Success;
							}

							current = constructor();

							return callback(this, state, ref current)
								? BrowserState.Continue
								: BrowserState.Failure;
						};
					}

					// Ignore array when not supported by current descriptor
					else
						return this.Skip(state) ? Browser<TElement>.EmptySuccess : Browser<TElement>.EmptyFailure;
			}
		}

		public bool ReadToObject<TObject>(ReaderState state,
			ILookup<int, ReaderCallback<ReaderState, JSONValue, TObject>> fields, ref TObject target)
		{
			state.PullIgnored();

			switch (state.Current)
			{
				case '[':
					return this.ReadToObjectFromArray(state, fields, ref target);

				case '{':
					return this.ReadToObjectFromObject(state, fields, ref target);

				default:
					return this.Skip(state);
			}
		}

		public bool ReadToValue(ReaderState state, out JSONValue value)
		{
			state.PullIgnored();

			switch (state.Current)
			{
				case '"':
					return Reader.ReadToValueFromString(state, out value);

				case '-':
				case '.':
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
					return Reader.ReadToValueFromNumber(state, out value);

				case 'f':
					state.Read();

					if (!state.PullExpected('a') || !state.PullExpected('l') || !state.PullExpected('s') || !state.PullExpected('e'))
					{
						value = default;

						return false;
					}

					value = JSONValue.FromBoolean(false);

					return true;

				case 'n':
					state.Read();

					if (!state.PullExpected('u') || !state.PullExpected('l') || !state.PullExpected('l'))
					{
						value = default;

						return false;
					}

					value = JSONValue.Void;

					return true;

				case 't':
					state.Read();

					if (!state.PullExpected('r') || !state.PullExpected('u') || !state.PullExpected('e'))
					{
						value = default;

						return false;
					}

					value = JSONValue.FromBoolean(true);

					return true;

				case '[':
					value = default;

					return this.Skip(state);

				case '{':
					value = default;

					return this.Skip(state);

				default:
					state.Error("expected array, object or value");

					value = default;

					return false;
			}
		}

		public ReaderState Start(Stream stream, ErrorEvent error)
		{
			return new ReaderState(stream, this.encoding, error);
		}

		public void Stop(ReaderState state)
		{
			state.Dispose();
		}

		public bool TryDecode<TEntity>(ReaderState state, ReaderCallback<ReaderState, JSONValue, TEntity> callback, Func<TEntity> constructor, out TEntity entity)
		{
			// If it is null the decoder should behave as if the element was not present at all.
			// So test null to prevent the call to the constructor() and to create the corresponding entity.
			if (state.Current == 'n')
			{
				entity = default;
				state.Read();
				return state.PullExpected('u') && state.PullExpected('l') && state.PullExpected('l');
			}
			else
			{
				entity = constructor();
				return callback(this, state, ref entity);
			}
		}

		private BrowserMove<TElement> ReadToArrayFromArray<TElement>(ReaderState state, Func<TElement> constructor,
			ReaderCallback<ReaderState, JSONValue, TElement> callback)
		{
			state.Read();

			return (int index, out TElement current) =>
			{
				state.PullIgnored();

				if (state.Current == ']')
				{
					state.Read();

					current = default;

					return BrowserState.Success;
				}

				// Read comma separator if any
				if (index > 0)
				{
					if (!state.PullExpected(','))
					{
						current = default;

						return BrowserState.Failure;
					}

					state.PullIgnored();
				}

				// Read array value
				current = constructor();

				return callback(this, state, ref current) ? BrowserState.Continue : BrowserState.Failure;
			};
		}

		private BrowserMove<TElement> ReadToArrayFromObjectValues<TElement>(ReaderState state, Func<TElement> constructor,
			ReaderCallback<ReaderState, JSONValue, TElement> callback)
		{
			state.Read();

			return (int index, out TElement current) =>
			{
				state.PullIgnored();

				if (state.Current == '}')
				{
					state.Read();

					current = default;

					return BrowserState.Success;
				}

				// Read comma separator if any
				if (index > 0)
				{
					if (!state.PullExpected(','))
					{
						current = default;

						return BrowserState.Failure;
					}

					state.PullIgnored();
				}

				if (!state.PullExpected('"'))
				{
					current = default;

					return BrowserState.Failure;
				}

				// Read and move to object key
				while (state.Current != '"')
				{
					if (!state.PullCharacter(out _))
					{
						state.Error("invalid character in object key");

						current = default;

						return BrowserState.Failure;
					}
				}

				state.Read();

				// Read object separator
				state.PullIgnored();

				if (!state.PullExpected(':'))
				{
					current = default;

					return BrowserState.Failure;
				}

				// Read object value
				state.PullIgnored();

				// Read array value
				current = constructor();

				return callback(this, state, ref current) ? BrowserState.Continue : BrowserState.Failure;
			};
		}

		private bool ReadToObjectFromArray<TObject>(ReaderState state, ILookup<int, ReaderCallback<ReaderState, JSONValue, TObject>> fields, ref TObject target)
		{
			state.Read();

			for (var index = 0;; ++index)
			{
				state.PullIgnored();

				if (state.Current == ']')
					break;

				// Read comma separator if any
				if (index > 0)
				{
					if (!state.PullExpected(','))
						return false;

					state.PullIgnored();
				}

				// Build and move to array index
				var field = fields;

				if (index > 9)
				{
					foreach (char digit in index.ToString(CultureInfo.InvariantCulture))
						field = field.Follow(digit);
				}
				else
					field = field.Follow((char)('0' + index));

				// Read array value
				if (!(field.HasValue ? field.Value(this, state, ref target) : this.Skip(state)))
					return false;
			}

			state.Read();

			return true;
		}

		private bool ReadToObjectFromObject<TObject>(ReaderState state,
			ILookup<int, ReaderCallback<ReaderState, JSONValue, TObject>> fields, ref TObject target)
		{
			state.Read();

			for (var index = 0;; ++index)
			{
				state.PullIgnored();

				if (state.Current == '}')
					break;

				// Read comma separator if any
				if (index > 0)
				{
					if (!state.PullExpected(','))
						return false;

					state.PullIgnored();
				}

				if (!state.PullExpected('"'))
					return false;

				// Read and move to object key
				var field = fields;

				while (state.Current != '"')
				{
					if (!state.PullCharacter(out var character))
					{
						state.Error("invalid character in object key");

						return false;
					}

					field = field.Follow(character);
				}

				state.Read();

				// Read object separator
				state.PullIgnored();

				if (!state.PullExpected(':'))
					return false;

				// Read object value
				state.PullIgnored();

				// If it is null the decoder should behave as if the element was not present at all.
				// So test null to prevent the call to field.Value() and to create the corresponding entity.
				if (state.Current == 'n')
				{
					state.Read();
					if (!(state.PullExpected('u') && state.PullExpected('l') && state.PullExpected('l')))
					{
						return false;
					}
				}
				else
				{
					if (!(field.HasValue ? field.Value(this, state, ref target) : this.Skip(state)))
						return false;
				}
			}

			state.Read();

			return true;
		}

		private static bool ReadToValueFromNumber(ReaderState state, out JSONValue value)
		{
			unchecked
			{
				const ulong mantissaMax = long.MaxValue / 10;

				var numberMantissa = 0UL;
				var numberPower = 0;

				// Read number sign
				ulong numberMantissaMask;
				ulong numberMantissaPlus;

				if (state.Current == '-')
				{
					state.Read();

					numberMantissaMask = ~0UL;
					numberMantissaPlus = 1;
				}
				else
				{
					numberMantissaMask = 0;
					numberMantissaPlus = 0;
				}

				// Read integral part
				for (; state.Current >= (int)'0' && state.Current <= (int)'9'; state.Read())
				{
					if (numberMantissa > mantissaMax)
					{
						++numberPower;

						continue;
					}

					numberMantissa = numberMantissa * 10 + (ulong)(state.Current - '0');
				}

				// Read decimal part if any
				if (state.Current == '.')
				{
					state.Read();

					for (; state.Current >= (int)'0' && state.Current <= (int)'9'; state.Read())
					{
						if (numberMantissa > mantissaMax)
							continue;

						numberMantissa = numberMantissa * 10 + (ulong)(state.Current - '0');

						--numberPower;
					}
				}

				// Read exponent if any
				if (state.Current == 'E' || state.Current == 'e')
				{
					uint numberExponentMask;
					uint numberExponentPlus;

					state.Read();

					switch (state.Current)
					{
						case '+':
							state.Read();

							numberExponentMask = 0;
							numberExponentPlus = 0;

							break;

						case '-':
							state.Read();

							numberExponentMask = ~0U;
							numberExponentPlus = 1;

							break;

						default:
							numberExponentMask = 0;
							numberExponentPlus = 0;

							break;
					}

					uint numberExponent;

					for (numberExponent = 0; state.Current >= (int)'0' && state.Current <= (int)'9'; state.Read())
						numberExponent = numberExponent * 10 + (uint)(state.Current - '0');

					numberPower += (int)((numberExponent ^ numberExponentMask) + numberExponentPlus);
				}

				// Compute result number and store as JSON value
				var number = (long)((numberMantissa ^ numberMantissaMask) + numberMantissaPlus) * Math.Pow(10, numberPower);

				value = JSONValue.FromNumber(number);

				return true;
			}
		}

		private static bool ReadToValueFromString(ReaderState state, out JSONValue value)
		{
			state.Read();

			var buffer = new StringBuilder(32);

			while (state.Current != '"')
			{
				if (!state.PullCharacter(out var character))
				{
					state.Error("invalid character in string value");

					value = default;

					return false;
				}

				buffer.Append(character);
			}

			state.Read();

			value = JSONValue.FromString(buffer.ToString());

			return true;
		}

		private bool Skip(ReaderState state)
		{
			var empty = false;

			switch (state.Current)
			{
				case '"':
					state.Read();

					while (state.Current != '"')
					{
						if (state.PullCharacter(out _))
							continue;

						state.Error("invalid character in string value");

						return false;
					}

					state.Read();

					return true;

				case '-':
				case '.':
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
					return Reader.ReadToValueFromNumber(state, out _);

				case 'f':
					state.Read();

					return state.PullExpected('a') && state.PullExpected('l') && state.PullExpected('s') && state.PullExpected('e');

				case 'n':
					state.Read();

					return state.PullExpected('u') && state.PullExpected('l') && state.PullExpected('l');

				case 't':
					state.Read();

					return state.PullExpected('r') && state.PullExpected('u') && state.PullExpected('e');

				case '[':
					return this.ReadToObjectFromArray(state, NameLookup<ReaderCallback<ReaderState, JSONValue, bool>>.Empty, ref empty);

				case '{':
					return this.ReadToObjectFromObject(state, NameLookup<ReaderCallback<ReaderState, JSONValue, bool>>.Empty, ref empty);

				default:
					state.Error("expected array, object or value");

					return false;
			}
		}
	}
}

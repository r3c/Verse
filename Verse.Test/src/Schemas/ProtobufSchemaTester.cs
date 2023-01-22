using System.IO;
using NUnit.Framework;
using Verse.Schemas;

namespace Verse.Test.Schemas
{
	[TestFixture]
	internal class ProtobufSchemaTester
	{
		[Test]
		//[TestCase("res/Protobuf/Example2.proto", "outer")]
		[TestCase("res/Protobuf/Example3.proto", "outer")]
		[TestCase("res/Protobuf/Person.proto", "Person")]
		public void Decode(string path, string messageName)
		{
			var proto = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, path));
			var schema = new ProtobufSchema<int>(new StringReader(proto), messageName);

			Assert.NotNull(schema);
		}

		private class Person
		{
			public string Email;
			public int Id;
			public string Name;
		}

		[Test]
		[Ignore("Proto messages are not supported yet")]
		public void DecodeAssign()
		{
			var proto = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "res/Protobuf/Person.proto"));
			var schema = new ProtobufSchema<Person>(new StringReader(proto), "Person");

			schema.DecoderDescriptor.HasField("email", () => string.Empty, (ref Person p, string v) => p.Email = v)
				.HasValue(schema.DecoderAdapter.ToString);
			schema.DecoderDescriptor.HasField("id", () => 0, (ref Person p, int v) => p.Id = v)
				.HasValue(schema.DecoderAdapter.ToInteger32S);
			schema.DecoderDescriptor.HasField("name", () => string.Empty, (ref Person p, string v) => p.Name = v)
				.HasValue(schema.DecoderAdapter.ToString);

			var decoder = schema.CreateDecoder(() => new Person());

			using (var stream = new MemoryStream(new byte[] { 16, 17, 0, 0, 0 }))
			{
				using (var decoderStream = decoder.Open(stream))
				{
					Assert.True(decoderStream.TryDecode(out var entity));
					Assert.AreEqual(17, entity.Id);
				}
			}
		}	
	}
}

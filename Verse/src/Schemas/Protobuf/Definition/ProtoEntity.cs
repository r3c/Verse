﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Verse.Schemas.Protobuf.Definition
{
    struct ProtoEntity
    {
        public readonly ProtoContainer Container;

        public readonly List<ProtoEntity> Entities;

        public readonly List<ProtoField> Fields;

        public readonly List<ProtoLabel> Labels;

        public readonly string Name;

        public ProtoEntity(ProtoContainer container, string name)
        {
            this.Container = container;
            this.Entities = new List<ProtoEntity>();
            this.Fields = new List<ProtoField>();
            this.Labels = new List<ProtoLabel>();
            this.Name = name;
        }

        public ProtoBinding[] Resolve(string name)
        {
            int index = this.Entities.FindIndex(d => d.Name == name);

            if (index < 0)
                throw new ResolverException("can't find message '{0}'", name);

            var entity = this.Entities[index];

            return this.ResolveEntity(entity, new [] { this, entity });
        }

        private ProtoBinding[] ResolveEntity(ProtoEntity entity, IEnumerable<ProtoEntity> parents)
        {
            var bindings = new ProtoBinding[0];

            foreach (ProtoField field in entity.Fields)
            {
                if (bindings.Length <= field.Number)
                    Array.Resize(ref bindings, field.Number + 1);

                bindings[field.Number] = this.ResolveField(field, parents);
            }

            return bindings;
        }

        private ProtoBinding ResolveField(ProtoField field, IEnumerable<ProtoEntity> parents)
        {
            if (field.Reference.Type != ProtoType.Custom)
                return new ProtoBinding(field.Name, field.Reference.Type);

            for (var stack = new Stack<ProtoEntity>(parents); stack.Count > 0; stack.Pop())
            {
                var entity = stack.Peek();
                var found = true;
                var match = new List<ProtoEntity>(stack);

                foreach (string name in field.Reference.Names)
                {
                    int index = entity.Entities.FindIndex(e => e.Name == name);

                    if (index < 0)
                    {
                        found = false;

                        break;
                    }

                    entity = entity.Entities[index];
                    match.Add(entity);
                }

                if (found)
                {
                    if (entity.Container == ProtoContainer.Enum)
                        return new ProtoBinding(field.Name, ProtoType.Int32);
        
                    return new ProtoBinding(field.Name, this.ResolveEntity(entity, match));
                }
            }

            throw new ResolverException("field '{0}' has undefined type '{1}'", field.Name, string.Join(".", field.Reference.Names));
        }
    }
}

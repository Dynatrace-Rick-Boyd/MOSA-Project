﻿/*
 * (c) 2014 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Ki (kiootic) <kiootic@gmail.com>
 *  Stefan Andres Charsley (charsleysa) <charsleysa@gmail.com>
 */

using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Mosa.Compiler.Common;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mosa.Compiler.MosaTypeSystem.Metadata
{
	internal class MetadataResolver
	{
		private CLRMetadata metadata;

		public MetadataResolver(CLRMetadata metadata)
		{
			this.metadata = metadata;
		}

		private Queue<MosaUnit> resolveQueue = new Queue<MosaUnit>();
		private Queue<MosaType> arrayResolveQueue = new Queue<MosaType>();

		public void EnqueueForResolve(MosaUnit unit)
		{
			resolveQueue.Enqueue(unit);
		}

		public void EnqueueForArrayResolve(MosaType type)
		{
			arrayResolveQueue.Enqueue(type);
		}

		public void Resolve()
		{
			foreach (var unit in metadata.Loader.LoadedUnits)
			{
				if (unit is MosaType)
				{
					MosaType type = (MosaType)unit;
					using (var mosaType = metadata.Controller.MutateType(type))
					{
						TypeDef typeDef = type.GetUnderlyingObject<UnitDesc<TypeDef, TypeSig>>().Definition;

						if (typeDef.BaseType != null)
							mosaType.BaseType = metadata.Loader.GetType(typeDef.BaseType.ToTypeSig());

						if (typeDef.DeclaringType != null)
							mosaType.DeclaringType = metadata.Loader.GetType(typeDef.DeclaringType.ToTypeSig());

						if (typeDef.IsEnum)
							mosaType.ElementType = metadata.Loader.GetType(typeDef.GetEnumUnderlyingType());

						foreach (var iface in typeDef.Interfaces)
							mosaType.Interfaces.Add(metadata.Loader.GetType(iface.Interface.ToTypeSig()));

						if (typeDef.BaseType != null)
							ResolveInterfacesInBaseTypes(mosaType, type.BaseType);
					}
					ResolveType(type);
				}
				else if (unit is MosaField || unit is MosaMethod || unit is MosaModule || unit is MosaProperty)
					resolveQueue.Enqueue(unit);
			}

			while (resolveQueue.Count > 0)
			{
				MosaUnit unit = resolveQueue.Dequeue();
				if (unit is MosaType)
					ResolveType((MosaType)unit);
				if (unit is MosaField)
					ResolveField((MosaField)unit);
				if (unit is MosaMethod)
					ResolveMethod((MosaMethod)unit);
				if (unit is MosaProperty)
					ResolveProperty((MosaProperty)unit);
				if (unit is MosaModule)
				{
					MosaModule module = (MosaModule)unit;
					using (var mosaModule = metadata.Controller.MutateModule(module))
						ResolveCustomAttributes(mosaModule, module.GetUnderlyingObject<UnitDesc<ModuleDef, object>>().Definition);
				}
			}

			foreach (var module in metadata.Cache.Modules.Values)
			{
				ModuleDef moduleDef = module.GetUnderlyingObject<UnitDesc<ModuleDef, object>>().Definition;
				if (moduleDef.EntryPoint != null)
				{
					using (var mosaModule = metadata.Controller.MutateModule(module))
						mosaModule.EntryPoint = metadata.Cache.GetMethodByToken(new ScopedToken(moduleDef, moduleDef.EntryPoint.MDToken));
				}
			}

			while (arrayResolveQueue.Count > 0)
			{
				MosaType type = arrayResolveQueue.Dequeue();
				type.FinishSZArray();
			}
		}

		private void ResolveInterfacesInBaseTypes(MosaType.Mutator mosaType, MosaType baseType)
		{
			foreach (MosaType iface in baseType.Interfaces)
			{
				if (mosaType.Interfaces.Contains(iface))
					continue;

				mosaType.Interfaces.Add(iface);
			}

			if (baseType.BaseType != null)
				ResolveInterfacesInBaseTypes(mosaType, baseType.BaseType);
		}

		private MosaCustomAttribute.Argument ToMosaCAArgument(CAArgument arg)
		{
			var value = arg.Value;
			if (value is UTF8String)
			{
				value = ((UTF8String)value).String;
			}
			else if (value is TypeSig)
			{
				value = metadata.Loader.GetType((TypeSig)value);
			}
			else if (value is CAArgument[])
			{
				var valueArray = (CAArgument[])value;
				var resultArray = new MosaCustomAttribute.Argument[valueArray.Length];
				for (int i = 0; i < resultArray.Length; i++)
					resultArray[i] = ToMosaCAArgument(valueArray[i]);
			}

			return new MosaCustomAttribute.Argument(metadata.Loader.GetType(arg.Type), value);
		}

		private void ResolveCustomAttributes(MosaUnit.MutatorBase unit, IHasCustomAttribute obj)
		{
			foreach (var attr in obj.CustomAttributes)
			{
				MosaType type = metadata.Loader.GetType(attr.AttributeType.ToTypeSig());
				MethodDef ctor = ((IMethodDefOrRef)attr.Constructor).ResolveMethod();
				MosaMethod mosaCtor = null;
				foreach (var method in type.Methods)
				{
					var desc = method.GetUnderlyingObject<UnitDesc<MethodDef, MethodSig>>();
					if (desc.Token.Token == ctor.MDToken)
					{
						mosaCtor = method;
						break;
					}
				}
				if (mosaCtor == null)
					throw new AssemblyLoadException();

				var values = new MosaCustomAttribute.Argument[attr.ConstructorArguments.Count];
				for (int i = 0; i < values.Length; i++)
					values[i] = ToMosaCAArgument(attr.ConstructorArguments[i]);

				var namedArgs = new MosaCustomAttribute.NamedArgument[attr.NamedArguments.Count];
				for (int i = 0; i < namedArgs.Length; i++)
				{
					var namedArg = attr.NamedArguments[i];
					namedArgs[i] = new MosaCustomAttribute.NamedArgument(namedArg.Name, namedArg.IsField, ToMosaCAArgument(namedArg.Argument));
				}

				unit.CustomAttributes.Add(new MosaCustomAttribute(mosaCtor, values, namedArgs));
			}
		}

		private void ResolveType(MosaType type)
		{
			GenericArgumentResolver resolver = new GenericArgumentResolver();

			MosaType srcType = type;
			if (type.GenericArguments.Count > 0)
			{
				resolver.PushTypeGenericArguments(type.GenericArguments.GetGenericArguments());
				srcType = type.ElementType;
				Debug.Assert(srcType != null);
			}

			using (var mosaType = metadata.Controller.MutateType(type))
			{
				if (srcType.BaseType != null)
					mosaType.BaseType = metadata.Loader.GetType(resolver.Resolve(srcType.BaseType.GetTypeSig()));

				if (srcType.DeclaringType != null)
				{
					mosaType.DeclaringType = metadata.Loader.GetType(resolver.Resolve(srcType.DeclaringType.GetTypeSig()));
					mosaType.Namespace = srcType.DeclaringType.Namespace;
				}

				var ifaces = new List<MosaType>(srcType.Interfaces);
				mosaType.Interfaces.Clear();
				for (int i = 0; i < ifaces.Count; i++)
					mosaType.Interfaces.Add(metadata.Loader.GetType(resolver.Resolve(ifaces[i].GetTypeSig())));

				mosaType.HasOpenGenericParams = type.GetTypeSig().HasOpenGenericParameter();

				ResolveCustomAttributes(mosaType, srcType.GetUnderlyingObject<UnitDesc<TypeDef, TypeSig>>().Definition);
			}

			// Add type again to make it easier to find
			metadata.Controller.AddType(type);
		}

		private void ResolveField(MosaField field)
		{
			GenericArgumentResolver resolver = new GenericArgumentResolver();

			if (field.DeclaringType.GenericArguments.Count > 0)
				resolver.PushTypeGenericArguments(field.DeclaringType.GenericArguments.GetGenericArguments());

			using (var mosaField = metadata.Controller.MutateField(field))
			{
				mosaField.FieldType = metadata.Loader.GetType(resolver.Resolve(field.GetFieldSig().Type));

				mosaField.HasOpenGenericParams =
					field.DeclaringType.HasOpenGenericParams ||
					field.FieldType.GetTypeSig().HasOpenGenericParameter();

				ResolveCustomAttributes(mosaField, field.GetUnderlyingObject<UnitDesc<FieldDef, FieldSig>>().Definition);
			}
		}

		private void ResolveProperty(MosaProperty property)
		{
			GenericArgumentResolver resolver = new GenericArgumentResolver();

			if (property.DeclaringType.GenericArguments.Count > 0)
				resolver.PushTypeGenericArguments(property.DeclaringType.GenericArguments.GetGenericArguments());

			using (var mosaProperty = metadata.Controller.MutateProperty(property))
			{
				mosaProperty.PropertyType = metadata.Loader.GetType(resolver.Resolve(property.GetPropertySig().RetType));

				ResolveCustomAttributes(mosaProperty, property.GetUnderlyingObject<UnitDesc<PropertyDef, PropertySig>>().Definition);
			}
		}

		private void ResolveMethod(MosaMethod method)
		{
			GenericArgumentResolver resolver = new GenericArgumentResolver();
			bool hasOpening = method.DeclaringType.HasOpenGenericParams;

			if (method.DeclaringType.GenericArguments.Count > 0)
			{
				foreach (var i in method.DeclaringType.GenericArguments.GetGenericArguments())
					hasOpening |= i.HasOpenGenericParameter();
				resolver.PushTypeGenericArguments(method.DeclaringType.GenericArguments.GetGenericArguments());
			}

			if (method.GenericArguments.Count > 0)
			{
				foreach (var i in method.GenericArguments.GetGenericArguments())
					hasOpening |= i.HasOpenGenericParameter();
				resolver.PushMethodGenericArguments(method.GenericArguments.GetGenericArguments());
			}

			using (var mosaMethod = metadata.Controller.MutateMethod(method))
			{
				var desc = method.GetUnderlyingObject<UnitDesc<MethodDef, MethodSig>>();

				MosaType returnType = metadata.Loader.GetType(resolver.Resolve(desc.Signature.RetType));
				hasOpening |= returnType.HasOpenGenericParams;
				List<MosaParameter> pars = new List<MosaParameter>();

				Debug.Assert(desc.Signature.GetParamCount() + (desc.Signature.HasThis ? 1 : 0) == desc.Definition.Parameters.Count);
				foreach (var param in desc.Definition.Parameters)
				{
					if (!param.IsNormalMethodParameter)
						continue;
					var paramType = metadata.Loader.GetType(resolver.Resolve(desc.Signature.Params[param.MethodSigIndex]));
					var parameter = metadata.Controller.CreateParameter();

					using (var mosaParameter = metadata.Controller.MutateParameter(parameter))
					{
						mosaParameter.Name = param.Name;
						mosaParameter.ParameterAttributes = (MosaParameterAttributes)param.ParamDef.Attributes;
						mosaParameter.ParameterType = paramType;
						mosaParameter.DeclaringMethod = method;
						ResolveCustomAttributes(mosaParameter, param.ParamDef);
					}

					pars.Add(parameter);
					hasOpening |= paramType.HasOpenGenericParams;
				}

				mosaMethod.Signature = new MosaMethodSignature(returnType, pars);

				foreach (var methodImpl in desc.Definition.Overrides)
				{
					Debug.Assert(methodImpl.MethodBody == desc.Definition);
					mosaMethod.Overrides.Add(ResolveMethodOperand(methodImpl.MethodDeclaration, null));
				}

				if (desc.Definition.HasBody)
					ResolveBody(desc.Definition, mosaMethod, desc.Definition.Body, resolver);

				mosaMethod.HasOpenGenericParams = hasOpening;

				ResolveCustomAttributes(mosaMethod, desc.Definition);
			}
		}

		private static int ResolveOffset(CilBody body, Instruction instruction)
		{
			if (instruction == null)
			{
				instruction = body.Instructions[body.Instructions.Count - 1];
				return (int)(instruction.Offset + instruction.GetSize());
			}
			else
				return (int)instruction.Offset;
		}

		private void ResolveBody(MethodDef methodDef, MosaMethod.Mutator method, CilBody body, GenericArgumentResolver resolver)
		{
			method.LocalVariables.Clear();
			int index = 0;
			foreach (var variable in body.Variables)
			{
				method.LocalVariables.Add(new MosaLocal(
					variable.Name ?? "V_" + index,
					metadata.Loader.GetType(resolver.Resolve(variable.Type)),
					variable.Type.IsPinned));
				index++;
			}

			method.ExceptionBlocks.Clear();
			foreach (var eh in body.ExceptionHandlers)
			{
				method.ExceptionBlocks.Add(new MosaExceptionHandler(
					(ExceptionHandlerType)eh.HandlerType,
					ResolveOffset(body, eh.TryStart),
					ResolveOffset(body, eh.TryEnd),
					ResolveOffset(body, eh.HandlerStart),
					ResolveOffset(body, eh.HandlerEnd),
					eh.CatchType == null ? null : metadata.Loader.GetType(resolver.Resolve(eh.CatchType.ToTypeSig())),
					eh.FilterStart == null ? null : (int?)eh.FilterStart.Offset
				));
			}

			method.MaxStack = methodDef.Body.MaxStack;

			method.Code.Clear();
			for (int i = 0; i < body.Instructions.Count; i++)
			{
				method.Code.Add(ResolveInstruction(methodDef, body, i, resolver));
			}
		}

		private MosaInstruction ResolveInstruction(MethodDef methodDef, CilBody body, int index, GenericArgumentResolver resolver)
		{
			Instruction instruction = body.Instructions[index];
			int? prev = index == 0 ? null : (int?)body.Instructions[index - 1].Offset;
			int? next = index == body.Instructions.Count - 1 ? null : (int?)body.Instructions[index + 1].Offset;

			object operand = instruction.Operand;

			// Special case: newarr instructions need to have their operand changed now so that the type is a SZArray
			if (instruction.OpCode == OpCodes.Newarr)
			{
				var typeSig = resolver.Resolve(((ITypeDefOrRef)instruction.Operand).ToTypeSig());
				var szArraySig = new SZArraySig(typeSig);
				operand = metadata.Loader.GetType(szArraySig);
			}
			else if (instruction.Operand is ITypeDefOrRef)
			{
				operand = ResolveTypeOperand((ITypeDefOrRef)instruction.Operand, resolver);
			}
			else if (instruction.Operand is MemberRef)
			{
				MemberRef memberRef = (MemberRef)instruction.Operand;
				if (memberRef.IsFieldRef)
					operand = ResolveFieldOperand(memberRef, resolver);
				else
					operand = ResolveMethodOperand(memberRef, resolver);
			}
			else if (instruction.Operand is IField)
			{
				operand = ResolveFieldOperand((IField)instruction.Operand, resolver);
			}
			else if (instruction.Operand is IMethod)
			{
				operand = ResolveMethodOperand((IMethod)instruction.Operand, resolver);
			}
			else if (instruction.Operand is Local)
			{
				operand = ((Local)instruction.Operand).Index;
			}
			else if (instruction.Operand is Parameter)
			{
				operand = ((Parameter)instruction.Operand).Index;
			}
			else if (instruction.Operand is Instruction)
			{
				operand = (int)((Instruction)instruction.Operand).Offset;
			}
			else if (instruction.Operand is Instruction[])
			{
				Instruction[] targets = (Instruction[])instruction.Operand;
				int[] offsets = new int[targets.Length];
				for (int i = 0; i < offsets.Length; i++)
					offsets[i] = (int)targets[i].Offset;
				operand = offsets;
			}
			else if (instruction.Operand is string)
			{
				operand = metadata.Cache.GetStringId((string)instruction.Operand);
			}

			ushort code = (ushort)instruction.OpCode.Code;
			if (code > 0xff)    // To match compiler's opcode values
				code = (ushort)(0x100 + (code & 0xff));

			return new MosaInstruction((int)instruction.Offset, code, operand, prev, next);
		}

		private MosaField ResolveFieldOperand(IField operand, GenericArgumentResolver resolver)
		{
			TypeSig declType;
			FieldDef fieldDef = operand as FieldDef;
			if (fieldDef == null)
			{
				MemberRef memberRef = (MemberRef)operand;
				fieldDef = memberRef.ResolveFieldThrow();
				declType = memberRef.DeclaringType.ToTypeSig();
			}
			else
				declType = fieldDef.DeclaringType.ToTypeSig();

			MDToken fieldToken = fieldDef.MDToken;

			MosaType type = metadata.Loader.GetType(resolver.Resolve(declType));
			foreach (var field in type.Fields)
			{
				var desc = field.GetUnderlyingObject<UnitDesc<FieldDef, FieldSig>>();
				if (desc.Token.Token == fieldToken)
				{
					return field;
				}
			}
			throw new AssemblyLoadException();
		}

		private MosaMethod ResolveArrayMethod(IMethod method, GenericArgumentResolver resolver)
		{
			MosaType type = metadata.Loader.GetType(resolver.Resolve(method.DeclaringType.ToTypeSig()));
			if (method.Name == "Get")
				return type.FindMethodByName("Get");
			else if (method.Name == "Set")
				return type.FindMethodByName("Set");
			else if (method.Name == "AddressOf")
				return type.FindMethodByName("AddressOf");
			else if (method.Name == ".ctor")
				return type.FindMethodByName(".ctor");
			else
				throw new AssemblyLoadException();
		}

		private MosaMethod ResolveMethodOperand(IMethod operand, GenericArgumentResolver resolver)
		{
			if (operand is MethodSpec)
				return metadata.Loader.LoadGenericMethodInstance((MethodSpec)operand, resolver);
			else if (operand.DeclaringType.TryGetArraySig() != null || operand.DeclaringType.TryGetSZArraySig() != null)
				return ResolveArrayMethod(operand, resolver);

			TypeSig declType;
			MethodDef methodDef = operand as MethodDef;
			if (methodDef == null)
			{
				MemberRef memberRef = (MemberRef)operand;
				methodDef = memberRef.ResolveMethodThrow();
				declType = memberRef.DeclaringType.ToTypeSig();
			}
			else
				declType = methodDef.DeclaringType.ToTypeSig();

			if (resolver != null)
				declType = resolver.Resolve(declType);

			MDToken methodToken = methodDef.MDToken;

			MosaType type = metadata.Loader.GetType(declType);
			foreach (var method in type.Methods)
			{
				var desc = method.GetUnderlyingObject<UnitDesc<MethodDef, MethodSig>>();
				if (desc.Token.Token == methodToken)
				{
					return method;
				}
			}

			throw new AssemblyLoadException();
		}

		private MosaType ResolveTypeOperand(ITypeDefOrRef operand, GenericArgumentResolver resolver)
		{
			return metadata.Loader.GetType(resolver.Resolve(operand.ToTypeSig()));
		}
	}
}

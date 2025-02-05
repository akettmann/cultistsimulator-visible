﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Fucine;
using OrbCreationExtensions;

namespace SecretHistories.Fucine
{

    
    public class DictImporter:AbstractImporter
    {

        public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> _cachedFucinePropertyToPopulate, EntityData entityData, ContentImportLog log)
        {
            //If no value can be found, initialise the property with a default instance of the correct type, then return
            EntityData subEntityData = entityData.ValuesTable[_cachedFucinePropertyToPopulate.LowerCaseName] as EntityData;


            if (subEntityData == null)
            {
                Type typeForDefaultSubEntity = _cachedFucinePropertyToPopulate.ThisPropInfo.PropertyType;
                _cachedFucinePropertyToPopulate.SetViaFastInvoke(entity, FactoryInstantiator.CreateObjectWithDefaultConstructor(typeForDefaultSubEntity));
                return false;
            }


            //possibilities:
            //Dictionary<string,string> 
            //Dictionary<string,int> 
            //Dictionary<string,T>
            //Dictionary<string,List<T>> where T is an IEntityUnique (and might be a QuickSpecEntity)
            //Dictionary<string,List<T>> where T is an IEntityAnonymous (and might be a QuickSpecEntity)

            //Check for / warn against any dictionaries without string as a key.


            var dictAttribute = _cachedFucinePropertyToPopulate.FucineAttribute as FucineDict;
            var entityProperties = TypeInfoCache<T>.GetCachedFucinePropertiesForType();



            //a hashtable of <id: listofmorphdetails>
            //eg, {fatiguing:husk} or eg: {fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}],exiling:[{id:exiled,morpheffect:mutate},{id:liberated,morpheffect:mutate}]}
            Type dictType = _cachedFucinePropertyToPopulate.ThisPropInfo.PropertyType;
            Type dictMemberType = dictType.GetGenericArguments()[1];


            IDictionary dict =   FactoryInstantiator.CreateObjectWithDefaultConstructor(dictType) as IDictionary;

            if (dictMemberType == typeof(string))
            {
                PopulateAsDictionaryOfStrings(entity, _cachedFucinePropertyToPopulate, subEntityData, dict, log);
            }
            else if (dictMemberType == typeof(int))
            {
                PopulateAsDictionaryOfInts(entity, _cachedFucinePropertyToPopulate, subEntityData, dict, log);
            }


            else if (dictMemberType.IsGenericType && dictMemberType.GetGenericTypeDefinition() == typeof(List<>))
            {
                PopulateAsDictionaryOfLists(entity, _cachedFucinePropertyToPopulate, dictMemberType, subEntityData, dict,log);
            }


            else //it's an entity, not a string or a list
            {
                PopulateAsDictionaryOfEntities(entity, _cachedFucinePropertyToPopulate, subEntityData, dictMemberType, dict, log);
            }

            if(dictAttribute!=null && dict!=null)
              ValidateKeysMustExistIn(entity, _cachedFucinePropertyToPopulate, dictAttribute.KeyMustExistIn, entityProperties, dict.Keys,log);

            return true;
        }

        public void PopulateAsDictionaryOfStrings<T>(T entity, CachedFucineProperty<T> _cachedFucinePropertyToPopulate, EntityData subEntityData, IDictionary dictionary,ContentImportLog log) where T:AbstractEntity<T>
        {
            //Dictionary<string,string> - like DrawMessages
                foreach (DictionaryEntry de in subEntityData.ValuesTable)
                {
                    dictionary.Add(de.Key, de.Value.ToString()); //if an aspect value rather than a rich aspect value has been passed in, we need to convert it back to a strjng
                }

                _cachedFucinePropertyToPopulate.SetViaFastInvoke(entity, dictionary);
        }

        public void PopulateAsDictionaryOfInts<T>(T entity, CachedFucineProperty<T> _cachedFucinePropertyToPopulate, EntityData subEntityData, IDictionary dictionary, ContentImportLog log) where T : AbstractEntity<T>
        {
            //Dictionary<string,int> - like HaltVerbs
            foreach (DictionaryEntry de in subEntityData.ValuesTable)
            {
                int value = Int32.Parse(de.Value.ToString());
                dictionary.Add(de.Key, value);
            }

            _cachedFucinePropertyToPopulate.SetViaFastInvoke(entity,dictionary);
        }


        private void PopulateAsDictionaryOfLists<T>(T entity, CachedFucineProperty<T> _cachedFucinePropertyToPopulate, Type wrapperListType, EntityData subEntityData, IDictionary dict, ContentImportLog log) where T: AbstractEntity<T>
        {
            //if Dictionary<T,List<T>> where T: entity then first create a wrapper list, then populate it with the individual entities //List<MorphDetails>, yup
            Type listMemberType = wrapperListType.GetGenericArguments()[0];
            //if it's {fatiguing:husk}, then it's a hashtable. If it's {fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}], then it's also a hashtable.
            //either way, it's implicit keys: fatiguing, exiling... 
            foreach (string dictKeyForList in subEntityData.ValuesTable.Keys)
            {
                IList wrapperList = FactoryInstantiator.CreateObjectWithDefaultConstructor(wrapperListType) as IList;

                //if it's potentially a QuickSpecEntity 
                if (listMemberType.GetInterfaces().Contains(typeof(IQuickSpecEntity)) && (subEntityData.ValuesTable[dictKeyForList] is string quickSpecEntityValue))
                {
                    //quick spec entities started out as a simple key:value pair, e.g. {fatiguing:husk}, but later had their possible definition extended to be potentially more complex, e.g fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}]
                    //it's a quick spec entity if (i) it implements IQuickSpecEntity (ii) the value resolves to a string (rather than a list)
                    AddQuickSpecEntityToWrapperList(listMemberType, quickSpecEntityValue, wrapperList,log);
                }

                //either it's not a quickspeccable entity, or it's a quickspeccableentity whose json resolves to a full list 
                else if (subEntityData.ValuesTable[dictKeyForList] is ArrayList list
                ) //fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}]
                {
                    AddFullSpecEntitiesToWrapperList(list, listMemberType, wrapperList,log);
                }
                else
                {
                    throw new ApplicationException(
                        $"FucineDictionary {_cachedFucinePropertyToPopulate.LowerCaseName} on {entity.GetType().Name} is a List<T>, but the <T> isn't drawing from strings or hashtables, but rather a {subEntityData.ValuesTable[dictKeyForList].GetType().Name}");
                }

                dict.Add(dictKeyForList, wrapperList); //{fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}]
            }

            _cachedFucinePropertyToPopulate.SetViaFastInvoke(entity, dict);
        }

        private static void AddQuickSpecEntityToWrapperList(Type listMemberType, string quickSpecEntityValue,
            IList wrapperList, ContentImportLog log)
        {
            // eg {fatiguing:husk}
            IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(listMemberType) as IQuickSpecEntity;
            quickSpecEntity.QuickSpec(quickSpecEntityValue);
            wrapperList.Add(
                quickSpecEntity); //this is just the value/effect, eg :husk, wrapped up in a more complex object in a list. So the list will only contain this one object

        }

        private void AddFullSpecEntitiesToWrapperList(ArrayList list, Type listMemberType, IList wrapperList, ContentImportLog log)
        {
            foreach (EntityData entityData in list)
            {
                IEntityWithId
                    sub =FactoryInstantiator.CreateEntity(listMemberType, entityData, log);

                wrapperList.Add(sub);
            }

            //list is now: [{ id: husk,morpheffect: spawn}, {id: smoke,morpheffect: spawn}]
        }



        private void PopulateAsDictionaryOfEntities<T>(T entity, CachedFucineProperty<T> _cachedFucinePropertyToPopulate, EntityData subEntityData, Type dictMemberType, IDictionary dict,ContentImportLog log) where T:AbstractEntity<T>
        {
            foreach (object o in subEntityData.ValuesTable)
            {
                if (o is Hashtable h) //if the arraylist contains hashtables, then it contains subentities / emanations
                {
                //{fatiguing:[{id:husk,morpheffect:spawn},{id:smoke,morpheffect:spawn}]
                    IEntityWithId sub = FactoryInstantiator.CreateEntity(dictMemberType, new EntityData(), log);
                    dict.Add(sub.Id, sub);
                   _cachedFucinePropertyToPopulate.SetViaFastInvoke(entity, dict);
                }
                else
                {
                    //we would hit this branch with subentities, like Expulsion, that don't have an id of their own
                    throw new ApplicationException(
                        $"FucineDictionary {_cachedFucinePropertyToPopulate.LowerCaseName} on {entity.GetType().Name} isn't a List<T>, a string, or drawing from a hashtable / IEntity - we don't know how to treat a {o.GetType().Name}");
                }
            }
        }
    }
}
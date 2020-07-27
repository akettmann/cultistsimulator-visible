﻿using System.Collections.Generic;
using System.Linq;
using Assets.Core.Interfaces;

public class EntityStore
{
    private Dictionary<string, IEntityWithId> _entities=new Dictionary<string, IEntityWithId>();


    public bool TryAddEntity(IEntityWithId entityToAdd)
    {
        if (!_entities.ContainsKey(entityToAdd.Id))
        {
            AddEntity(entityToAdd);
            return true;
        }

        return false;
    }


    public void AddEntity(IEntityWithId entityToAdd)
    {
        _entities.Add(entityToAdd.Id, entityToAdd);

    }

    public bool TryGetById<T>(string entityId, out T entity) where T : class, IEntityWithId
    {
        IEntityWithId retrievedEntity;
        if(_entities.TryGetValue(entityId, out retrievedEntity))
        {
            entity = retrievedEntity as T;
            return true;
        }
        else
        {
            entity = null;
            return false;
        }


    }


    public T GetById<T>(string entityId) where T : class, IEntityWithId
    {
        return _entities[entityId] as T;
    }


    public List<IEntityWithId> GetAllAsList()
    {
        return new List<IEntityWithId>(_entities.Values);
    }


    public List<T> GetAllAsList<T>() where T: class, IEntityWithId
    {
        
        return new List<T>(_entities.Values.Cast<T>().ToList());
    }

    public Dictionary<string, IEntityWithId> GetAll()
    {
        return new Dictionary<string, IEntityWithId>(_entities);
    }
}
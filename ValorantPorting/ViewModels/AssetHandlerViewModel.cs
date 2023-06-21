﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using ValorantPorting.AppUtils;
using ValorantPorting.Views.Controls;
using ValorantPorting.Views.Extensions;
using Serilog;
using ZstdSharp.Unsafe;

namespace ValorantPorting.ViewModels;

public class AssetHandlerViewModel
{
    public readonly Dictionary<EAssetType, AssetHandlerData> Handlers ;


    public AssetHandlerViewModel()
    {
        Handlers = new Dictionary<EAssetType, AssetHandlerData>
        {
            { EAssetType.Character, _characterHandler },
            { EAssetType.Weapon, _weaponHandler },
            { EAssetType.GunBuddy, _buddyHandler },
            { EAssetType.Map, _mapHandler }
        };

    }

    private readonly AssetHandlerData _characterHandler = new()
    {
        AssetType = EAssetType.Character,
        TargetCollection = AppVM.MainVM.Outfits,
        ClassNames = new List<string> { "CharacterDataAsset" },
        IconGetter = UI_Asset =>
        {
            UI_Asset.TryGetValue(out UTexture2D? previewImage, "DisplayIcon");
            return previewImage;
        }
    };

    private readonly AssetHandlerData _weaponHandler = new()
    {
        AssetType = EAssetType.Weapon,
        TargetCollection = AppVM.MainVM.Weapons,
        ClassNames = new List<string> { "EquippableSkinDataAsset" },
        IconGetter = UI_Asset =>
        {
            UI_Asset.TryGetValue(out UTexture2D? previewImage, "DisplayIcon");
            return previewImage;
        }
    };


    private readonly AssetHandlerData _buddyHandler = new()
    {
        AssetType = EAssetType.GunBuddy,
        TargetCollection = AppVM.MainVM.Gunbuddies,
        ClassNames = new List<string> { "EquippableCharmDataAsset" },
        IconGetter = UI_Asset =>
        {
            UI_Asset.TryGetValue(out UTexture2D? previewImage, "DisplayIcon");
            return previewImage;
        }
    };
    private readonly AssetHandlerData _mapHandler = new()
    {
        AssetType = EAssetType.Map,
        TargetCollection = AppVM.MainVM.Maps,
        ClassNames = new List<string> { "MapDataAsset" },
        IconGetter = UI_Asset =>
        {
            UI_Asset.TryGetValue(out UTexture2D? previewImage, "DisplayIcon");
            return previewImage;
        }
    };
    
    private readonly AssetHandlerData _abilityHandler = new()
    {
        //AssetType = EAssetType.Map,
        TargetCollection = AppVM.MainVM.Maps,
        ClassNames = new List<string> { "EquippableDataAsset" },
        IconGetter = UI_Asset =>
        {
            UI_Asset.TryGetValue(out UTexture2D? previewImage, "LevelSplashScreen");
            return previewImage;
        }
    };
    
    public async Task Initialize()
    {
        await _characterHandler.Execute(); // default tab
    }
}


public class AssetHandlerData
{
    public bool HasStarted { get; private set; }
    public Pauser PauseState { get; } = new();
    
    public EAssetType AssetType;
    public ObservableCollection<AssetSelectorItem> TargetCollection;
    public List<string> ClassNames;
    public Func<UObject, UTexture2D?> IconGetter;

    public async Task Execute()
    {
        if (HasStarted) return;
        HasStarted = true;
        var items = new List<FAssetData>();
        foreach (var variable in AppVM.CUE4ParseVM.AssetRegistry.PreallocatedAssetDataBuffers) //search for Classes in AssetRegistry
        {
            foreach (var tagsAndValue in variable.TagsAndValues)
            {
                //if (variable.AssetName.PlainText.Contains("Ascent_PrimaryAsset_C")) Console.WriteLine(tagsAndValue.Value);
                if (ClassNames.Contains(tagsAndValue.Value) && tagsAndValue.Key.PlainText == "PrimaryAssetType") items.Add(variable);
            }
        }
        await Parallel.ForEachAsync(items, async (data, token) => //load if found
        {
            await DoLoad(data);
        });
    }
    
    private async Task DoLoad(FAssetData data, bool random = false)
    {
        await PauseState.WaitIfPaused();
        UObject actualAsset = new UObject();
        UObject uiAsset =  new UObject();
        var firstTag = data.ObjectPath;
        
        if (firstTag.Contains("NPE") || firstTag.Contains("Random")) return;

        actualAsset = await AppVM.CUE4ParseVM.Provider.TryLoadObjectAsync(firstTag);
        if (actualAsset == null) return;
        
        var uBlueprintGeneratedClass = actualAsset as UBlueprintGeneratedClass;
        actualAsset = uBlueprintGeneratedClass.ClassDefaultObject.Load();
        var mainA = actualAsset;
        
        if (actualAsset.TryGetValue(out UBlueprintGeneratedClass uiObject, "UIData"))
        {
            uiAsset = uiObject.ClassDefaultObject.Load();
        }
        // switch on asset type
        string loadable = "None";
        switch (AssetType)
        {
            case EAssetType.Character:
                loadable = "Character";
                break;
            case EAssetType.Weapon:
                actualAsset.TryGetValue<UBlueprintGeneratedClass[]>(out var bGg, "Levels");
                actualAsset = bGg[0].ClassDefaultObject.Load();
                loadable = "None";
                break;
            case EAssetType.GunBuddy:
                actualAsset.TryGetValue<UBlueprintGeneratedClass[]>(out var bGb, "Levels");
                actualAsset = bGb[0].ClassDefaultObject.Load();
                loadable = "CharmAttachment";
                break;
        }
    
        if (actualAsset.TryGetValue(out UBlueprintGeneratedClass blueprintObject, loadable))
        {
            actualAsset = blueprintObject.ClassDefaultObject.Load();
        }
        var previewImage = IconGetter(uiAsset);
        if (previewImage is null) return;
        await Application.Current.Dispatcher.InvokeAsync(() => TargetCollection.Add(new AssetSelectorItem(actualAsset, uiAsset,mainA ,previewImage, random)), DispatcherPriority.Background);
    }
}
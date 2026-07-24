using MaIN.Domain.Models.Abstract;
using MaIN.Domain.Models.Concrete;

namespace MaIN.Core.UnitTests;

public class LocalModelAssetsTests
{
    [Fact]
    public void RequiredAssets_ForSingleFileModel_ContainsOnlyMainFile()
    {
        var model = new GenericLocalModel("model.gguf");

        var assets = model.RequiredAssets.ToList();

        Assert.Single(assets);
        Assert.Equal("model.gguf", assets[0].FileName);
    }

    [Fact]
    public void RequiredAssets_ForMultiAssetDiffusionModel_IncludesMainFileAndConfiguredAssets()
    {
        var model = new GenericLocalImageGenerationModel(
            "sd3.5_large-Q4_0.gguf",
            DiffusionArchitecture.SD3,
            Vae: new ModelAsset("sd3.5_vae.safetensors"),
            ClipL: new ModelAsset("clip_l.safetensors"),
            ClipG: new ModelAsset("clip_g.safetensors"),
            T5Xxl: new ModelAsset("t5xxl_fp8_e4m3fn.safetensors"));

        var assets = model.RequiredAssets.Select(a => a.FileName).ToList();

        Assert.Equal(
            ["sd3.5_large-Q4_0.gguf", "sd3.5_vae.safetensors", "clip_l.safetensors", "clip_g.safetensors", "t5xxl_fp8_e4m3fn.safetensors"],
            assets);
    }

    [Fact]
    public void RequiredAssets_ForQwen3VisionModel_IncludesModelAndProjector()
    {
        var model = new Qwen3_VL_32b_Instruct();

        var assets = model.RequiredAssets.ToList();

        Assert.Equal([model.FileName, model.MMProjectName], assets.Select(asset => asset.FileName));
        Assert.All(assets, asset => Assert.NotNull(asset.DownloadUrl));
    }

    [Fact]
    public void IsDownloaded_ForSingleFileModel_TrueOnlyWhenFileExists()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var model = new GenericLocalModel("model.gguf");

            Assert.False(model.IsDownloaded(dir.FullName));

            File.WriteAllText(Path.Combine(dir.FullName, "model.gguf"), "data");

            Assert.True(model.IsDownloaded(dir.FullName));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsDownloaded_ForMultiAssetModel_RequiresAllAssetsPresent()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var model = new GenericLocalImageGenerationModel(
                "diffusion.gguf",
                DiffusionArchitecture.Flux,
                Vae: new ModelAsset("ae.safetensors"));

            File.WriteAllText(Path.Combine(dir.FullName, "diffusion.gguf"), "data");
            Assert.False(model.IsDownloaded(dir.FullName));

            File.WriteAllText(Path.Combine(dir.FullName, "ae.safetensors"), "data");
            Assert.True(model.IsDownloaded(dir.FullName));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsDownloaded_WithoutCustomPathOrBasePath_ReturnsFalse()
    {
        var model = new GenericLocalModel("model.gguf");

        Assert.False(model.IsDownloaded(null));
    }
}

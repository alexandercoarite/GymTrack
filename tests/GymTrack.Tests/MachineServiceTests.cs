using GymTrack.Application.Common;
using GymTrack.Application.DTOs;
using GymTrack.Domain.Enums;

namespace GymTrack.Tests;

public class MachineServiceTests
{
    private static MachineEditModel NewModel(string name = "Prensa") => new()
    {
        Nombre = name, Descripcion = "Descripción", CategoryId = 1, MuscleGroupId = 1, Tipo = TrainingType.Fuerza
    };

    [Fact]
    public async Task Admin_can_create_and_search_machines()
    {
        using var db = new TestDb();
        var service = db.Machines();
        var id = await service.CreateAsync(NewModel());
        var found = await service.SearchAsync(new MachineFilter { Texto = "pren" });
        Assert.Single(found);
        Assert.Equal(id, found[0].Id);
        Assert.Equal(MachineStatus.Disponible, found[0].Estado);
    }

    [Fact]
    public async Task Non_admin_cannot_create_machines()
    {
        using var db = new TestDb();
        db.User.Admin = false;
        await Assert.ThrowsAsync<ForbiddenException>(() => db.Machines().CreateAsync(NewModel()));
    }

    [Fact]
    public async Task Duplicate_names_are_rejected()
    {
        using var db = new TestDb();
        var service = db.Machines();
        await service.CreateAsync(NewModel());
        await Assert.ThrowsAsync<ServiceException>(() => service.CreateAsync(NewModel("prensa")));
    }

    [Fact]
    public async Task Invalid_model_is_rejected_by_server_validation()
    {
        using var db = new TestDb();
        var model = NewModel();
        model.Nombre = "";
        await Assert.ThrowsAsync<ServiceException>(() => db.Machines().CreateAsync(model));
    }

    [Fact]
    public async Task Update_with_stale_version_fails()
    {
        using var db = new TestDb();
        var service = db.Machines();
        var id = await service.CreateAsync(NewModel());
        var first = MachineEditModel.From((await service.GetAsync(id))!);
        var second = MachineEditModel.From((await service.GetAsync(id))!);

        first.Nombre = "Prensa 45";
        await service.UpdateAsync(first);

        second.Nombre = "Otro nombre";
        await Assert.ThrowsAsync<ServiceException>(() => service.UpdateAsync(second));
    }

    [Fact]
    public async Task Delete_removes_machine_and_its_image()
    {
        using var db = new TestDb();
        var images = new FakeImageStorage();
        var service = db.Machines(images);
        var model = NewModel();
        model.ImagenRuta = "/uploads/machines/test.jpg";
        var id = await service.CreateAsync(model);

        await service.DeleteAsync(id);

        Assert.Null(await service.GetAsync(id));
        Assert.Contains("/uploads/machines/test.jpg", images.Deleted);
    }

    [Fact]
    public async Task Maintenance_flow_updates_machine_status()
    {
        using var db = new TestDb();
        var machines = db.Machines();
        var id = await machines.CreateAsync(NewModel());
        var maintenance = new GymTrack.Infrastructure.Services.MaintenanceService(db.Factory, db.User, db.Audit());

        var recordId = await maintenance.RegisterAsync(new MaintenanceCreateModel { MachineId = id, Descripcion = "Cable roto" });
        Assert.Equal(MachineStatus.EnMantenimiento, (await machines.GetAsync(id))!.Estado);

        await maintenance.ResolveAsync(recordId, "Cable reemplazado");
        Assert.Equal(MachineStatus.Disponible, (await machines.GetAsync(id))!.Estado);
    }

    [Fact]
    public async Task Status_cannot_be_set_to_maintenance_without_a_record()
    {
        using var db = new TestDb();
        var service = db.Machines();
        var id = await service.CreateAsync(NewModel());
        await Assert.ThrowsAsync<ServiceException>(() => service.ChangeStatusAsync(id, MachineStatus.EnMantenimiento));
    }
}

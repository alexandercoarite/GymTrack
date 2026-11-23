using GymTrack.Application.Common;
using GymTrack.Application.DTOs;
using GymTrack.Infrastructure.Services;

namespace GymTrack.Tests;

public class OpeningHourServiceTests
{
    [Fact]
    public async Task Week_has_seven_days_even_when_empty()
    {
        using var db = new TestDb();
        var service = new OpeningHourService(db.Factory, db.User, db.Audit(), new FakeClock());
        Assert.Equal(7, (await service.GetWeekAsync()).Count);
    }

    [Fact]
    public async Task Closing_time_must_be_after_opening_time()
    {
        using var db = new TestDb();
        var service = new OpeningHourService(db.Factory, db.User, db.Audit(), new FakeClock());
        var week = (await service.GetWeekAsync()).ToList();
        week[0].Abierto = true;
        week[0].Apertura = new TimeOnly(10, 0);
        week[0].Cierre = new TimeOnly(9, 0);
        await Assert.ThrowsAsync<ServiceException>(() => service.SaveWeekAsync(week));
    }

    [Fact]
    public async Task Saved_week_is_returned_and_status_reflects_current_time()
    {
        using var db = new TestDb();
        var clock = new FakeClock(); // lunes 10:00
        var service = new OpeningHourService(db.Factory, db.User, db.Audit(), clock);
        var week = (await service.GetWeekAsync()).ToList();
        week[0].Abierto = true;
        week[0].Apertura = new TimeOnly(6, 0);
        week[0].Cierre = new TimeOnly(22, 0);
        await service.SaveWeekAsync(week);

        Assert.True((await service.GetTodayStatusAsync()).AbiertoAhora);
        clock.Now = new DateTime(2026, 11, 16, 23, 0, 0);
        Assert.False((await service.GetTodayStatusAsync()).AbiertoAhora);
    }

    [Fact]
    public async Task Special_closure_overrides_the_weekly_schedule()
    {
        using var db = new TestDb();
        var service = new OpeningHourService(db.Factory, db.User, db.Audit(), new FakeClock());
        var week = (await service.GetWeekAsync()).ToList();
        week[0].Abierto = true;
        week[0].Apertura = new TimeOnly(6, 0);
        week[0].Cierre = new TimeOnly(22, 0);
        await service.SaveWeekAsync(week);
        await service.SaveSpecialAsync(new SpecialOpeningHourModel
        {
            Fecha = new DateOnly(2026, 11, 16), Abierto = false, Descripcion = "Feriado"
        });

        Assert.False((await service.GetTodayStatusAsync()).AbiertoAhora);
    }
}

public class ProgressServiceTests
{
    [Fact]
    public async Task Adding_records_keeps_history()
    {
        using var db = new TestDb();
        var service = new ProgressService(db.Factory, db.User);
        await service.AddAsync(new ProgressModel { Fecha = new DateTime(2026, 11, 1), PesoKg = 80 });
        await service.AddAsync(new ProgressModel { Fecha = new DateTime(2026, 11, 8), PesoKg = 79.2 });

        var list = await service.ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal(80, list[0].PesoKg);
        Assert.Equal(79.2, list[1].PesoKg);
    }

    [Fact]
    public async Task Out_of_range_weight_is_rejected()
    {
        using var db = new TestDb();
        var service = new ProgressService(db.Factory, db.User);
        await Assert.ThrowsAsync<ServiceException>(() => service.AddAsync(new ProgressModel { PesoKg = 5 }));
    }

    [Fact]
    public async Task Users_only_see_their_own_records()
    {
        using var db = new TestDb();
        var service = new ProgressService(db.Factory, db.User);
        await service.AddAsync(new ProgressModel { Fecha = DateTime.Today, PesoKg = 70 });

        using (var ctx = db.Factory.CreateDbContext())
        {
            ctx.Users.Add(new GymTrack.Infrastructure.Identity.ApplicationUser
            {
                Id = "user-2", UserName = "user2", NormalizedUserName = "USER2", Nombre = "Luis", Apellido = "Otro"
            });
            ctx.SaveChanges();
        }
        db.User.UserId = "user-2";
        Assert.Empty(await service.ListAsync());
    }
}

public class ImageValidatorTests
{
    [Fact]
    public void Detects_jpeg_png_and_rejects_other_content()
    {
        Assert.Equal(".jpg", ImageValidator.DetectExtension(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal(".png", ImageValidator.DetectExtension(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        Assert.Null(ImageValidator.DetectExtension(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")));
    }

    [Fact]
    public void Oversized_images_are_rejected()
    {
        var big = new byte[3 * 1024 * 1024];
        big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;
        Assert.Throws<ServiceException>(() => ImageValidator.ValidateOrThrow(big, ImageValidator.MaxMachineImageBytes));
    }
}

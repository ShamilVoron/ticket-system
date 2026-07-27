using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITCafe.Api.Controllers;

/// <summary>Справочник отделов (хардкод, синхронизирован с frontend tickets/new).</summary>
[Authorize(Roles = StaffRoles.All)]
[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : ControllerBase
{
    /// <summary>
    /// Список отделов как на форме новой заявки.
    /// Возвращаем { value, label, desc } — frontend (settings/employees/new) ожидает эти поля.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<object>> GetAll()
    {
        return Ok(new object[]
        {
            new { value = "Координатор", label = "Координатор", desc = "Распределение, эскалации" },
            new { value = "1 линия", label = "1 линия", desc = "Приём, консультации" },
            new { value = "2 линия", label = "2 линия", desc = "Сложные вопросы" },
            new { value = "Разработчики", label = "Разработчики", desc = "Доработки, баги" },
            new { value = "Выездные инженеры", label = "Выездные инженеры", desc = "Выезд, монтаж" },
            new { value = "Ремонт / сервис", label = "Ремонт / сервис", desc = "Подменки, склад, сервисный центр" },
            new { value = "Бухгалтерия", label = "Бухгалтерия", desc = "Счета, акты" },
            new { value = "Закупки", label = "Закупки", desc = "Внешние поставки, контрагенты" },
            new { value = "Системный администратор", label = "Системный администратор", desc = "Инфраструктура, сервера, сеть" },
        });
    }
}

/// <summary>Роли сотрудников для [Authorize(Roles=…)].</summary>
public static class StaffRoles
{
    public const string All =
        "support_l1,support_l2,developer,field_engineer,accountant,super_admin,coordinator,sysadmin,head_support,head_dev,head_engineers,head_repair,director,procurement,agent";
}

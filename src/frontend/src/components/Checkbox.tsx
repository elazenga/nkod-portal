import { HTMLAttributes, useId } from "react";

interface IProps extends HTMLAttributes<HTMLInputElement>
{
    label: string;
    checked: boolean;
    onCheckedChange: (checked: boolean) => void;
}

export default function Checkbox(props: IProps)
{
    const id = useId();

    const { label, checked, onCheckedChange, ...inputProperties } = props;

    return <div className="govuk-checkboxes__item">
        <input className="govuk-checkboxes__input" id={id} onChange={e => props.onCheckedChange(e.target.checked)} checked={checked} type="checkbox" {...inputProperties} />
        <label className="govuk-label govuk-checkboxes__label" htmlFor={id}>
            {props.label}
        </label>
    </div>;
}
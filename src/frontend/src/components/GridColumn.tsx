import { HTMLAttributes } from "react";

interface IProps extends HTMLAttributes<HTMLDivElement>
{
    widthUnits: number;
    totalUnits: number;
}

export default function GridColumn(props: IProps)
{
    let autoClassName = 'govuk-grid-column-';

    const { widthUnits, totalUnits, className, ...attributes } = props;
    
    if (props.totalUnits <= 1 || props.widthUnits >= props.totalUnits)
    {
        autoClassName += 'full';
    } else if (props.widthUnits < props.totalUnits) {
        switch (props.widthUnits)
        {
            case 1:
                autoClassName += 'one-';
                break;
            case 2:
                autoClassName += 'two-';
                break;
            case 3:
                autoClassName += 'three-';
                break;
        }

        switch (props.totalUnits)
        {
            case 2:
                autoClassName += 'half';
                break;
            case 3:
                autoClassName += 'third';
                break;
            case 4:
                autoClassName += 'quarter';
                break;
        }

        if (props.widthUnits > 1)
        {
            autoClassName += 's';
        }
    }

    autoClassName += ' ';

    return <div className={autoClassName + (className ? className : '')} {...attributes}>
        {props.children}
    </div>
}